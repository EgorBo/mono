// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Data;
using System.Data.Common;

namespace System.Data.SqlClient
{
    partial class SqlConnection
    {
        [MonoTODO] //https://github.com/dotnet/corefx/issues/11958
        public static void ChangePassword (string connectionString, string newPassword)
		{
            throw new NotImplementedException();
        }
    }
}