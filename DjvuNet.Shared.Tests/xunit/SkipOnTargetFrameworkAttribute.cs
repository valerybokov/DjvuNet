// SPDX-License-Identifier: MIT
// Copyright (c) 2025 .NET Foundation
// Copyright 2026 © Jacek Błaszczyński

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit.v3;
using Xunit.NetCore.Extensions;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify this is a platform specific test.
    /// Provide valid framework strings, which can include OS, Architecture, and Pre-release tags 
    /// (e.g., "net10.0-arm64", "net11.0-preview2-arm64").
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class SkipOnTargetFrameworkAttribute : Attribute, ITraitAttribute
    {
        private readonly string[] _targetFrameworks;

        // Extracts base framework (e.g., net10.0) from its hyphenated modifiers (-windows, -arm64)
        private static readonly Regex BaseFormatRegex = new Regex(
            @"^(?<base>net(?:coreapp)?|netstandard|mono|nativeaot|netfx|uap)(?<version>\d+(?:\.\d+)*)?(?:-(?<modifiers>.*))?$", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public SkipOnTargetFrameworkAttribute(params string[] targetFrameworks)
        {
            if (targetFrameworks == null || targetFrameworks.Length == 0)
                throw new ArgumentException("You must provide at least one target framework moniker.", nameof(targetFrameworks));

            foreach (var tfm in targetFrameworks)
            {
                if (string.IsNullOrWhiteSpace(tfm))
                    throw new ArgumentException("Target framework string cannot be null or empty.");

                ValidateEnvironmentMoniker(tfm);
            }

            _targetFrameworks = targetFrameworks;
        }

        private static void ValidateEnvironmentMoniker(string tfm)
        {
            var match = BaseFormatRegex.Match(tfm);

            if (!match.Success)
            {
                throw new ArgumentException(
                    $"Invalid format: '{tfm}'. Must start with a known framework identifier " +
                    $"(e.g., 'net', 'netcoreapp', 'mono') followed by an optional version.");
            }

            string framework = match.Groups["base"].Value.ToLowerInvariant();
            string version = match.Groups["version"].Value;

            // Validate that modern 'net' TFMs include a minor version (net10.0 is valid, net10 is invalid)
            // Legacy net4x (e.g. net48) is exempt.
            if (framework == "net" && version.Length > 0 && !version.Contains(".") && !version.StartsWith("4"))
            {
                throw new ArgumentException($"Modern 'net' framework strings must include a minor version (e.g., '{tfm}.0', not '{tfm}').");
            }

            // If there are hyphenated modifiers (e.g., -windows10.0, -arm64, -preview2), strictly validate each one
            if (match.Groups["modifiers"].Success)
            {
                var modifiers = match.Groups["modifiers"].Value.Split('-');
                foreach (var modifier in modifiers)
                {
                    bool isValidModifier = 
                        Regex.IsMatch(modifier, @"^(windows|linux|osx|ios|android|macos|maccatalyst|tvos|tizen|browser)(?:\d+(?:\.\d+)*)?$", RegexOptions.IgnoreCase) || // OS + optional version
                        Regex.IsMatch(modifier, @"^(x64|x86|arm64|arm|wasm|s390x)$", RegexOptions.IgnoreCase) || // Architecture
                        Regex.IsMatch(modifier, @"^(preview|rc|alpha|beta)\.?\d*$", RegexOptions.IgnoreCase); // Pre-release

                    if (!isValidModifier)
                    {
                        throw new ArgumentException(
                            $"Invalid modifier '{modifier}' found in '{tfm}'. " +
                            $"Allowed modifiers are OS (e.g., 'windows', 'ios15.0'), Architecture ('x64', 'arm64'), or Pre-release ('preview2', 'rc1').");
                    }
                }
            }
        }

        public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        {
            var traits = new List<KeyValuePair<string, string>>();
            foreach (var tfm in _targetFrameworks)
            {
                traits.Add(new KeyValuePair<string, string>(XunitConstants.Category, $"skip-{tfm.ToLowerInvariant()}"));
            }
            return traits;
        }
    }
}