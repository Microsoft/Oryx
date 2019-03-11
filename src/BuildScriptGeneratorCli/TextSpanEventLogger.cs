// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.BuildScriptGenerator
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using Microsoft.Oryx.Common;

    /// <summary>
    /// Measures time for events within a textual stream.
    /// </summary>
    internal class TextSpanEventLogger
    {
        private readonly ILogger _logger;

        private readonly Dictionary<string, TextSpan> _beginnings = new Dictionary<string, TextSpan>();

        private readonly Dictionary<string, TextSpan> _endings = new Dictionary<string, TextSpan>();

        private readonly Dictionary<TextSpan, EventStopwatch> _events = new Dictionary<TextSpan, EventStopwatch>();

        public TextSpanEventLogger(ILogger logger, TextSpan[] events)
        {
            _logger = logger;

            foreach (var span in events)
            {
                _beginnings[span.BeginMarker] = span;
                _endings[span.EndMarker] = span;
            }
        }

        public void CheckString(string rawInput)
        {
            var marker = rawInput.Trim();

            if (_beginnings.ContainsKey(marker)) // Start measuring
            {
                var span = _beginnings[marker];
                if (!_events.ContainsKey(span)) // Avoid a new measurement for a span already being measured
                {
                    _events[span] = _logger.LogTimedEvent(span.Name);
                }
            }
            else if (_endings.ContainsKey(marker)) // Stop a running measurement
            {
                var span = _endings[marker];
                _events.GetValueOrDefault(span)?.Dispose(); // Records the measurement
                _events.Remove(span); // No need to check if the removal succeeded, because the event might not exist
            }
        }
    }
}
