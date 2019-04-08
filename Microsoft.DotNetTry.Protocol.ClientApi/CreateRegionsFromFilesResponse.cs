﻿using System;
using Newtonsoft.Json;

namespace Microsoft.DotNetTry.Protocol.ClientApi
{
    public class CreateRegionsFromFilesResponse : MessageBase
    {
        [JsonProperty("projections")]
        public SourceFileRegion[] Regions { get; }

        public CreateRegionsFromFilesResponse(string requestId, SourceFileRegion[] projections) : base(requestId)
        {
            Regions = projections ?? Array.Empty<SourceFileRegion>();
        }
    }
}