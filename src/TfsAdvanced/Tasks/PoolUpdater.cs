﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using TfsAdvanced.Data;
using TfsAdvanced.Data.Pools;
using TfsAdvanced.Data.Projects;
using TfsAdvanced.Repository;
using TfsAdvanced.Utilities;

namespace TfsAdvanced.Tasks
{
    public class PoolUpdater
    {
        private readonly RequestData requestData;
        private readonly PoolRepository poolRepository;
        private readonly UpdateStatusRepository updateStatusRepository;
        private bool IsRunning;


        public PoolUpdater(PoolRepository poolRepository, RequestData requestData, UpdateStatusRepository updateStatusRepository)
        {
            this.requestData = requestData;
            this.updateStatusRepository = updateStatusRepository;
            this.poolRepository = poolRepository;
        }

        [AutomaticRetry(Attempts = 0)]
        public void Update()
        {
            if (IsRunning)
                return;
            IsRunning = true;
            try
            {

                var pools = GetAsync.FetchResponseList<Pool>(requestData, $"{requestData.BaseAddress}/_apis/distributedtask/pools?api-version=1.0").Result;
                if (pools != null)
                {
                    poolRepository.UpdatePools(pools);
                    updateStatusRepository.UpdateStatus(new UpdateStatus {LastUpdate = DateTime.Now, UpdatedRecords = pools.Count, UpdaterName = nameof(PoolUpdater)});
                }

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error running Pool Updater", ex);
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}