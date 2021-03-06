﻿using ATDD.TestScriptor.BackendServices.Models;
using ATDD.TestScriptor.BackendServices.Services;
using ATDD.TestScriptor.Library;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ATDD.TestScriptor.BackendServices.Hubs
{
    public class ATDDHub : Hub<IATDDHub>
    {
        protected IALObjectService objectService { get; set; }

        public ATDDHub(IALObjectService alObjectService)
        {
            objectService = alObjectService;
        }

        public async Task QueryProjects(IEnumerable<string> msg)
        {
            var projects = ALProjectCollector.Discover(msg.ToList());

            await Clients.All.GetProjects(projects);
        }

        public async Task QueryObjects(IEnumerable<string> msg)
        {
            var result = await objectService.GetTests(msg);

            await Clients.All.GetObjects(result);
        }

        public async Task SaveChanges(MessageUpdate msg, Configurations config)
        {
            objectService.SaveChanges(msg, config);
            await Clients.All.SaveChangesResponse(true);
        }
        public async Task CheckSaveChanges(MessageUpdate msg, Configurations config)
        {
            bool procedureCanBeRemovedAfterwards = objectService.checkSaveChanges(msg, config);
            await Clients.All.CheckSaveChangesResponse(procedureCanBeRemovedAfterwards);
        }

    }
}
