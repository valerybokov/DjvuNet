// SPDX-License-Identifier: MIT
// Copyright (c) 2025 .NET Foundation
// Copyright 2026 © Jacek Błaszczyński

#nullable enable

using System.Runtime.InteropServices;

namespace Xunit
{
    /// <summary>
    /// This test should be run only on Windows.
    /// </summary>
    public class WindowsOnlyFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsOnlyFactAttribute"/> class.
        /// </summary>
        /// <param name="additionalMessage">The additional message that is appended to skip reason, when test is skipped.</param>
        public WindowsOnlyFactAttribute(string? additionalMessage = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Windows to run." + (string.IsNullOrWhiteSpace(additionalMessage) ? "" : " " + additionalMessage);
            }
        }

        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public WindowsOnlyFactAttribute(
            string? additionalMessage,
            [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            : base(filePath, lineNumber)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Windows to run." + (string.IsNullOrWhiteSpace(additionalMessage) ? "" : " " + additionalMessage);
            }
        }
    }
}

