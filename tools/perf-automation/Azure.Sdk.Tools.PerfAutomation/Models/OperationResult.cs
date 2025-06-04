// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class OperationResult
    {
        public TimeSpan ExecutionTime { get; set; }
        public long Size { get; set; }
    }
}
