﻿using System.Collections.Generic;
using BambooTray.Domain.Resources;

namespace BambooTray.Services
{
    public interface IBambooService
    {
        InfoResponse GetServerInfo();

        IList<PlanDetailResonse> GetAllPlans();

        PlanDetailResonse GetPlanDetail(string key);

        IList<Result> GetPlanResults(string key);

        ResultDetailResponse GetResultDetail(string key);
    }
}