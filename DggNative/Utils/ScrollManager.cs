using System;
using Gtk;

namespace DggNative.Utils
{
    /// <summary>
    /// Manages scroll position and auto-scroll behavior for the chat view
    /// </summary>
    public class ScrollManager
    {
        private readonly ScrolledWindow _scrolledWindow;
        private readonly double _threshold;
        private bool _isAtBottom;
        private bool _userScrolled;

        /// <summary>
        /// Gets whether the view should auto-scroll to bottom on new messages
        /// </summary>
        public bool ShouldAutoScroll => _isAtBottom && !_userScrolled;

        /// <summary>
        /// Initialize scroll manager for a ScrolledWindow
        /// </summary>
        /// <param name="scrolledWindow">The ScrolledWindow to manage</param>
        /// <param name="threshold">Pixel threshold from bottom to consider "at bottom" (default: 50)</param>
        public ScrollManager(ScrolledWindow scrolledWindow, double threshold = 50)
        {
            _scrolledWindow = scrolledWindow;
            _threshold = threshold;
            _isAtBottom = true;
            _userScrolled = false;

            // Subscribe to scroll position changes
            var vAdjustment = _scrolledWindow.Vadjustment;
            vAdjustment.ValueChanged += OnScrollValueChanged;
        }

        /// <summary>
        /// Scroll to the bottom of the view
        /// </summary>
        public void ScrollToBottom()
        {
            var vAdjustment = _scrolledWindow.Vadjustment;
            vAdjustment.Value = vAdjustment.Upper - vAdjustment.PageSize;
            _isAtBottom = true;
            _userScrolled = false;
        }

        /// <summary>
        /// Update scroll position tracking (call after adding new content)
        /// </summary>
        public void UpdateScrollPosition()
        {
            TrackScrollPosition();
        }

        /// <summary>
        /// Reset user scroll state (call when programmatically scrolling)
        /// </summary>
        public void ResetUserScrollState()
        {
            _userScrolled = false;
        }

        /// <summary>
        /// Track current scroll position to determine if at bottom
        /// </summary>
        private void TrackScrollPosition()
        {
            var vAdjustment = _scrolledWindow.Vadjustment;
            var currentPosition = vAdjustment.Value;
            var maxPosition = vAdjustment.Upper - vAdjustment.PageSize;

            // Consider "at bottom" if within threshold of the actual bottom
            _isAtBottom = (currentPosition >= maxPosition - _threshold);
        }

        /// <summary>
        /// Handle scroll value changes to detect user scrolling
        /// </summary>
        private void OnScrollValueChanged(object sender, EventArgs e)
        {
            var vAdjustment = _scrolledWindow.Vadjustment;
            var currentPosition = vAdjustment.Value;
            var maxPosition = vAdjustment.Upper - vAdjustment.PageSize;

            // Check if user scrolled up (away from bottom)
            if (currentPosition < maxPosition - _threshold)
            {
                _userScrolled = true;
                _isAtBottom = false;
            }
            else
            {
                // User scrolled back to bottom
                _isAtBottom = true;
                _userScrolled = false;
            }
        }
    }
}