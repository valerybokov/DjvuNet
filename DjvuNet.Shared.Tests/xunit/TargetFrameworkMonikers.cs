// SPDX-License-Identifier: MIT
// Copyright (c) 2025 .NET Foundation
// Copyright 2026 © Jacek Błaszczyński

using System;

namespace Xunit
{
    [Flags]
    public enum TargetFrameworkMonikers
    {
        Netcoreapp = 1,
        NetFramework = 2,
        Any = ~0
    }
}

