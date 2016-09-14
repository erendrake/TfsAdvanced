﻿using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TfsAdvanced.Data;
using TfsAdvanced.Data.Repositories;
using TfsAdvanced.Infrastructure;

namespace TfsAdvanced.ServiceRequests
{
    public class RepositoryServiceRequest
    {
        private readonly AppSettings appSettings;
        private Cache cache;
        private string REPOSITORY_LIST_MEMORY_KEY = "RepositoryListMemoryKey-";

        public RepositoryServiceRequest(IOptions<AppSettings> appSettings, Cache cache)
        {
            this.cache = cache;
            this.appSettings = appSettings.Value;
        }

        public string BuildDashboardURL(RequestData requestData, Repository repository)
        {
            return $"{requestData.BaseAddress}/{repository.project.name}/_dashboards";
        }

        public IList<Repository> GetAllRepositories(RequestData requestData)
        {
            IList<Repository> cached = cache.Get<IList<Repository>>(REPOSITORY_LIST_MEMORY_KEY + "all");
            if (cached != null)
                return cached;

            var concurrentRepositories = new ConcurrentStack<Repository>();
            Parallel.ForEach(appSettings.Projects, project =>
            {
                var repos = GetRepositories(requestData, project).Result;
                concurrentRepositories.PushRange(repos.ToArray());
            });
            var repositories = concurrentRepositories.ToList();
            cache.Put(REPOSITORY_LIST_MEMORY_KEY + "all", repositories, TimeSpan.FromHours(1));

            return repositories;
        }

        public async Task<IList<Repository>> GetRepositories(RequestData requestData, string tfsProject)
        {
            IList<Repository> cached = cache.Get<IList<Repository>>(REPOSITORY_LIST_MEMORY_KEY + tfsProject);
            if (cached != null)
                return cached;

            var response = await requestData.HttpClient.GetStringAsync($"{requestData.BaseAddress}/{tfsProject}/_apis/git/repositories?api=1.0");
            var responseObject = JsonConvert.DeserializeObject<Response<IEnumerable<Repository>>>(response);
            var repositories = responseObject.value.ToList();
            Parallel.ForEach(repositories, r => r.project.remoteUrl = BuildDashboardURL(requestData, r));

            cache.Put(REPOSITORY_LIST_MEMORY_KEY + tfsProject, repositories, TimeSpan.FromHours(1));

            return repositories;
        }
    }
}