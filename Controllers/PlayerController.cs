using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Mvc;
using Coflnet.Sky.Api.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// The playerController handles all player related endpoints.
    /// </summary>
    [ApiController]
    [Route("api/player")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class PlayerController : ControllerBase
    {
        const int pageSize = 10;
        HypixelContext context;
        private FilterEngine filterEngine;

        /// <summary>
        /// Creates a new instance of <see cref="PlayerController"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filterEngine"></param>
        public PlayerController(HypixelContext context, FilterEngine filterEngine)
        {
            this.context = context;
            this.filterEngine = filterEngine;
        }


        /// <summary>
        /// The last 10 bids (with auction) a player did
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <param name="page">Page of auctions (another 10)</param>
        /// <param name="filters"></param>
        /// <returns></returns>
        [Route("{playerUuid}/bids")]
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false, VaryByQueryKeys = new string[] { "*" })]
        public async Task<List<BidResult>> GetPlayerBids(string playerUuid, int page = 0, [FromQuery] Dictionary<string, string> filters = null)
        {
            AssertUuid(playerUuid);
            var offset = pageSize * page;
            var baseSelect = context.Bids.Where(b => b.BidderId == context.Players.Where(p => p.UuId == playerUuid).Select(p => p.Id).FirstOrDefault());
            filters.Remove("page");
            if (filters.TryGetValue("tag", out string itemTag))
            {
                int itemId = ExtractItemIdFromFilter(filters, itemTag);
                baseSelect = baseSelect.Where(b => b.Auction.ItemId == itemId);
            }
            if (filters != null && filters.Count > 0)
            {
                var expression = filterEngine.GetMatchExpression(filters, true);
                baseSelect = baseSelect.Select(b => b.Auction).Where(expression).Select(a => a.Bids.FirstOrDefault());
            }
            //.Include (p => p.Auction)
            var playerBids = await baseSelect.OrderByDescending(auction => auction.Id).Skip(offset)
                        .Take(pageSize).Select(b => new
                        {
                            b.Auction.Uuid,
                            b.Auction.ItemName,
                            b.Auction.Tag,
                            b.Auction.HighestBidAmount,
                            b.Auction.End,
                            b.Amount,
                            b.Auction.StartingBid,
                            b.Auction.Bin

                        }).GroupBy(b => b.Uuid)
                    .Select(bid => new
                    {
                        bid.Key,
                        Amount = bid.Max(b => b.Amount),
                        HighestBid = bid.Max(b => b.HighestBidAmount),
                        ItemName = bid.Max(b => b.ItemName),
                        Tag = bid.Max(b => b.Tag),
                        HighestOwnBid = bid.Max(b => b.Amount),
                        End = bid.Max(b => b.End),
                        StartBid = bid.Max(b => b.StartingBid),
                        Bin = bid.Max(b => b.Bin)
                    })
                    .ToListAsync();

            var aggregatedBids = playerBids
                        .Select(b => new BidResult()
                        {
                            HighestBid = b.HighestBid,
                            AuctionId = b.Key,
                            End = b.End,
                            HighestOwnBid = b.HighestOwnBid,
                            ItemName = b.ItemName,
                            Tag = b.Tag,
                            StartingBid = b.StartBid,
                            Bin = b.Bin
                        })
                        .OrderByDescending(b => b.End)
                        .ToList();
            return aggregatedBids;
        }

        /// <summary>
        /// The last 10 auctions a player created
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <param name="page">Page of auctions (another 10)</param>
        /// <param name="filters"></param>
        /// <returns></returns>
        [Route("{playerUuid}/auctions")]
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false, VaryByQueryKeys = new string[] { "*" })]
        public async Task<List<AuctionResult>> GetPlayerAuctions(string playerUuid, int page = 0, [FromQuery] Dictionary<string, string> filters = null)
        {
            AssertUuid(playerUuid);
            var offset = pageSize * page;
            filters.Remove("page");
            var baseSelect = context.Auctions
                        .Where(a => a.SellerId == context.Players.Where(p => p.UuId == playerUuid).Select(p => p.Id).FirstOrDefault());
            if (filters.TryGetValue("tag", out string itemTag))
            {
                int itemId = ExtractItemIdFromFilter(filters, itemTag);
                baseSelect = baseSelect.Where(a => a.ItemId == itemId);
            }
            if (filters != null && filters.Count > 0)
            {
                var expression = filterEngine.GetMatchExpression(filters, true);
                baseSelect = baseSelect.Where(expression);
            }
            var batch = await baseSelect
                        .OrderByDescending(a => a.Id)
                        .Skip(offset)
                        .Take(pageSize)
                        .ToListAsync();

            return batch.OrderByDescending(a => a.End)
                    .Select(a => new AuctionResult(a))
                    .ToList();
        }

        private static int ExtractItemIdFromFilter(Dictionary<string, string> filters, string itemTag)
        {
            var itemId = ItemDetails.Instance.GetItemIdForTag(itemTag);
            filters["ItemId"] = itemId.ToString();
            filters.Remove("tag");
            return itemId;
        }

        /// <summary>
        /// The name for a given uuid
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <returns></returns>
        [Route("{playerUuid}/name")]
        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<string> GetPlayerName(string playerUuid)
        {
            AssertUuid(playerUuid);
            return (await PlayerService.Instance.GetPlayer(playerUuid))?.Name ?? "unknown";
        }


        /// <summary>
        /// The name for a given uuid
        /// </summary>
        /// <param name="playerUuid">The uuid of the player</param>
        /// <returns></returns>
        [Route("{playerUuid}/name")]
        [HttpPost]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<string> UpdateName(string playerUuid)
        {
            AssertUuid(playerUuid);
            await IndexerClient.TriggerNameUpdate(playerUuid);
            return "ok";
        }

        private static void AssertUuid(string playerUuid)
        {
            if (playerUuid.Length != 32)
                throw new CoflnetException("invalid_uuid", "The provided string does not seem to be a valid minecraft account uuid.");
        }
    }
}

