using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RestSharp;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for flips
    /// </summary>
    [ApiController]
    [Route("api/flip")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class FlipController : ControllerBase
    {
        private IConfiguration config;

        /// <summary>
        /// Creates a new instance of <see cref="FlipController"/>
        /// </summary>
        /// <param name="config"></param>
        public FlipController(IConfiguration config)
        {
            this.config = config;
        }

        /// <summary>
        /// The last time an update was loaded (cached for 30min)
        /// You should only look at the second part
        /// </summary>
        /// <returns></returns>
        [Route("update/when")]
        [HttpGet]
        public async Task<DateTime> GetFlipTime()
        {
            return await new NextUpdateRetriever().Get();
        }

        /// <summary>
        /// Shows you the available settings options for the socket comand subFlip,
        /// Doesn't currently actually do anything.
        /// </summary>
        /// <returns>The default settings for modsocket v1</returns>
        [Route("settings/options")]
        [HttpGet]
        public FlipSettings SeeOptions()
        {
            return null;//Sky.Commands.MC.MinecraftSocket.DEFAULT_SETTINGS;
        }


        /// <summary>
        /// Callback for external flip finders to be included in tracking
        /// </summary>
        /// <param name="auctionId">Id of found and purchased auction</param>
        /// <param name="finder">Identifier of finder</param>
        /// <param name="playerId">The uuid of the player</param>
        /// <param name="price">Sugested target price</param>
        /// <returns></returns>
        [Route("track/purchase/{auctionId}")]
        [HttpPost]
        public async Task TrackExternalFlip(string auctionId, string finder, string playerId, int price = -1)
        {
            var received = DateTime.Now;
            await Sky.Commands.FlipTrackingService.Instance.NewFlip(new LowPricedAuction()
            {
                Auction = new hypixel.SaveAuction() { Uuid = auctionId },
                Finder = finder.ToLower() == "tfm" ? LowPricedAuction.FinderType.TFM : LowPricedAuction.FinderType.EXTERNAL,
                TargetPrice = price
            }, received);
            await Sky.Commands.FlipTrackingService.Instance.ReceiveFlip(auctionId, playerId);
        }

        /// <summary>
        /// Callback for external flip finders to be included in tracking. 
        /// </summary>
        /// <param name="finder">Identifier of finder</param>
        /// <param name="auctionId">The id of the found auction</param>
        /// <param name="price">Suggested target price</param>
        /// <returns></returns>
        [Route("track/found/{auctionId}")]
        [HttpPost]
        public async Task TrackExternalFlip(string auctionId, string finder, int price = -1)
        {
            await Sky.Commands.FlipTrackingService.Instance.NewFlip(new LowPricedAuction()
            {
                Auction = new hypixel.SaveAuction() { Uuid = auctionId },
                Finder = finder.ToLower() == "tfm" ? LowPricedAuction.FinderType.TFM : LowPricedAuction.FinderType.EXTERNAL,
                TargetPrice = price
            }, DateTime.Now);
        }


        /// <summary>
        /// Get flips stats for player
        /// </summary>
        /// <param name="playerUuid">Uuid of player to get stats for</param>
        /// <returns></returns>
        [Route("stats/player/{playerUuid}")]
        [HttpGet]
        [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<FlipSumary> GetStats(string playerUuid)
        {
            return await Sky.Commands.FlipTrackingService.Instance.GetPlayerFlips(playerUuid, TimeSpan.FromDays(7));
        }

        /// <summary>
        /// Get flips stats for player for the last hour (faster)
        /// </summary>
        /// <param name="playerUuid">Uuid of player</param>
        /// <returns></returns>
        [Route("stats/player/{playerUuid}/hour")]
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<FlipSumary> GetHourStats(string playerUuid)
        {
            return await Sky.Commands.FlipTrackingService.Instance.GetPlayerFlips(playerUuid, TimeSpan.FromHours(1));
        }


        /// <summary>
        /// Get flips stats for player
        /// </summary>
        /// <param name="finderName">Uuid of player to get stats for</param>
        /// <param name="start">The start time of flips to get (inclusive)</param>
        /// <param name="end">The end time of flips to get (exclusive)</param>
        /// <returns></returns>
        [Route("stats/finder/{finderName}")]
        [HttpGet]
        [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any, NoStore = false, VaryByQueryKeys = new string[] { "start", "end" })]
        public async Task<List<FlipDetails>> GetFlipsForFinder(string finderName, DateTime start = default, DateTime end = default)
        {
            if (end == default)
                end = DateTime.Now;
            if (start == default)
                start = end - TimeSpan.FromHours(1);
            Console.WriteLine(start);
            return await Sky.Commands.FlipTrackingService.Instance.GetFlipsForFinder(Enum.Parse<LowPricedAuction.FinderType>(finderName, true), start, end);
        }
    }
}

