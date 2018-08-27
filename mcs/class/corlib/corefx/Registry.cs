// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Win32
{
    partial class Registry
    {
        [Obsolete("The DynData registry key only works on Win9x, which is no longer supported. On NT-based operating systems, use the PerformanceData registry key instead.")]
        public static readonly RegistryKey DynData = RegistryKey.OpenBaseKey(RegistryHive.DynData, RegistryView.Default);
    }
}