using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public interface ICredentialStore
{
    Task<AuthCookies?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AuthCookies credentials, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class CredentialStoreUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public record CredentialLoadResult(AuthCookies? Credentials, string? Error = null);
public record CredentialOperationResult(bool Succeeded, string? Error = null);

public sealed class CredentialPersistenceService
{
    private readonly ICredentialStore _store;

    public CredentialPersistenceService(ICredentialStore store) => _store = store;

    public async Task<CredentialLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            return new CredentialLoadResult(stored is { HasCredentials: true } ? stored : null);
        }
        catch (CredentialStoreUnavailableException ex)
        {
            return new CredentialLoadResult(null, ex.Message);
        }
        catch (Exception ex)
        {
            return new CredentialLoadResult(null, $"Secure credential storage failed: {ex.GetType().Name}.");
        }
    }

    public async Task<CredentialOperationResult> SaveAsync(
        AuthCookies credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.SaveAsync(credentials, cancellationToken).ConfigureAwait(false);
            return new CredentialOperationResult(true);
        }
        catch (CredentialStoreUnavailableException ex)
        {
            return new CredentialOperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            return new CredentialOperationResult(false,
                $"Credentials could not be saved securely: {ex.GetType().Name}.");
        }
    }

    public async Task<CredentialOperationResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.ClearAsync(cancellationToken).ConfigureAwait(false);
            return new CredentialOperationResult(true);
        }
        catch (CredentialStoreUnavailableException ex)
        {
            return new CredentialOperationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            return new CredentialOperationResult(false,
                $"Credentials could not be removed: {ex.GetType().Name}.");
        }
    }

}

public static class CredentialStoreFactory
{
    public static ICredentialStore CreateDefault() =>
        OperatingSystem.IsWindows() ? new WindowsDpapiCredentialStore()
        : OperatingSystem.IsLinux() ? new LinuxSecretServiceCredentialStore()
        : new UnavailableCredentialStore("Secure credential storage is unsupported on this platform; login is session-only.");
}

public sealed class LinuxSecretServiceCredentialStore : ICredentialStore
{
    private const string Service = "DggNative";
    private const string Account = "official-destiny-gg";

    public async Task<AuthCookies?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["lookup", "service", Service, "account", Account], null, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == 1 && string.IsNullOrWhiteSpace(result.Error)) return null;
        if (result.ExitCode != 0) throw Unavailable(result.Error);
        if (string.IsNullOrWhiteSpace(result.Output)) return null;
        return JsonSerializer.Deserialize<AuthCookies>(result.Output);
    }

    public async Task SaveAsync(AuthCookies credentials, CancellationToken cancellationToken = default)
    {
        var secret = JsonSerializer.Serialize(credentials);
        var result = await RunAsync(
            ["store", "--label=DggNative official chat", "service", Service, "account", Account],
            secret,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) throw Unavailable(result.Error);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["clear", "service", Service, "account", Account], null, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == 1 && string.IsNullOrWhiteSpace(result.Error)) return;
        if (result.ExitCode != 0) throw Unavailable(result.Error);
    }

    private static async Task<ProcessResult> RunAsync(
        string[] arguments, string? input, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo("secret-tool")
            {
                RedirectStandardInput = input != null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo)
                ?? throw new CredentialStoreUnavailableException("Linux Secret Service is unavailable; login is session-only.");
            if (input != null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, output, error);
        }
        catch (Win32Exception ex)
        {
            throw new CredentialStoreUnavailableException(
                "Linux Secret Service is unavailable (secret-tool was not found); login is session-only.", ex);
        }
    }

    private static CredentialStoreUnavailableException Unavailable(string error) => new(
        string.IsNullOrWhiteSpace(error)
            ? "Linux Secret Service is unavailable; login is session-only."
            : "Linux Secret Service refused secure storage; login is session-only.");

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}

public sealed class UnavailableCredentialStore(string reason) : ICredentialStore
{
    public Task<AuthCookies?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<AuthCookies?>(new CredentialStoreUnavailableException(reason));
    public Task SaveAsync(AuthCookies credentials, CancellationToken cancellationToken = default) =>
        Task.FromException(new CredentialStoreUnavailableException(reason));
    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new CredentialStoreUnavailableException(reason));
}

public sealed class WindowsDpapiCredentialStore : ICredentialStore
{
    private readonly string _path;

    public WindowsDpapiCredentialStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DggNative", "credentials.bin");

    public async Task<AuthCookies?> LoadAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        if (!File.Exists(_path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        var clearBytes = Unprotect(protectedBytes);
        try { return JsonSerializer.Deserialize<AuthCookies>(clearBytes); }
        finally { CryptographicOperations.ZeroMemory(clearBytes); }
    }

    public async Task SaveAsync(AuthCookies credentials, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        var clearBytes = JsonSerializer.SerializeToUtf8Bytes(credentials);
        try
        {
            var protectedBytes = Protect(clearBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllBytesAsync(_path, protectedBytes, cancellationToken).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(clearBytes); }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI requires Windows.");
    }

    private static byte[] Protect(byte[] input) => Transform(input, CryptProtectData);
    private static byte[] Unprotect(byte[] input) => Transform(input, CryptUnprotectData);

    private static byte[] Transform(byte[] input, CryptTransform transform)
    {
        var inputPointer = Marshal.AllocHGlobal(input.Length);
        try
        {
            Marshal.Copy(input, 0, inputPointer, input.Length);
            var inputBlob = new DataBlob(input.Length, inputPointer);
            if (!transform(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out var outputBlob))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var output = new byte[outputBlob.Length];
                Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                return output;
            }
            finally { LocalFree(outputBlob.Data); }
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, inputPointer, input.Length);
            Marshal.FreeHGlobal(inputPointer);
        }
    }

    private delegate bool CryptTransform(ref DataBlob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;

        public DataBlob(int length, IntPtr data)
        {
            Length = length;
            Data = data;
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
