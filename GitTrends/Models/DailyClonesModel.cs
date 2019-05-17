﻿using System;
using Newtonsoft.Json;

namespace GitTrends
{
    class DailyClonesModel : BaseDailyModel
    {
        public DailyClonesModel(DateTimeOffset day, long totalViews, long totalUniqueViews) : base(day, totalViews, totalUniqueViews)
        {

        }

        [JsonIgnore]
        public long TotalViews => TotalCount;

        [JsonIgnore]
        public long TotalUniqueViews => TotalUniqueCount;
    }
}