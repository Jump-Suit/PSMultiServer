using QuazalServer.RDVServices.DDL.Models;
using QuazalServer.QNetZ.Attributes;
using QuazalServer.QNetZ.Interfaces;
using QuazalServer.RDVServices.RMC;

namespace QuazalServer.RDVServices.PS3UbisoftServices
{
    /// <summary>
    /// Ubi achievements service
    /// </summary>
    [RMCService(RMCProtocolId.UbiWinService)]
    public class UbiWinService : RMCServiceBase
	{
		[RMCMethod(1)]
		public RMCResult GetActions(int start_row_index, int maximum_rows, string sort_expression, string culture_name)
		{
            UNIMPLEMENTED();

            var result = new List<UplayAction>();
            return Result(result);
        }

		[RMCMethod(2)]
		public RMCResult GetActionsCompleted(int start_row_index, int maximum_rows, string sort_expression, string culture_name)
		{
            UNIMPLEMENTED();
            return Error(0);
        }

		[RMCMethod(3)]
        public RMCResult GetActionsCount(string platform_code, string game_code)
		{
            UNIMPLEMENTED();

            int actions_count = 0;
			return Result(new { actions_count });
		}

		[RMCMethod(4)]
		public RMCResult GetActionsCompletedCount(string platform_code, string game_code)
		{
            UNIMPLEMENTED();
            return Error(0);
        }

		[RMCMethod(5)]
		public RMCResult GetRewards(int start_row_index, int maximum_rows, string sort_expression, string culture_name)
		{
            UNIMPLEMENTED();
            return Error(0);
        }

		[RMCMethod(6)]
		public RMCResult GetRewardsPurchased(int startRowIndex, int maximumRows, string sortExpression, string cultureName, string platformCode)
		{
            UNIMPLEMENTED();

            var rewards = new List<UPlayReward>();

			// return 
			return Result(rewards);
		}

		[RMCMethod(7)]
		public RMCResult UplayWelcome(string culture, string platformCode)
        {
            var result = new List<UplayAction>();
			return Result(result);
		}

		[RMCMethod(8)]
		public RMCResult SetActionCompleted(string actionCode, string cultureName, string platformCode)
		{
			UNIMPLEMENTED();
			var unlockedAction = new UplayAction()
			{
				m_code = actionCode,
				m_description = actionCode + "_description",
				m_gameCode = "UNK",
				m_name = actionCode + "_action",
				m_value = 1,
			};
			unlockedAction.m_platforms.Add(new UplayActionPlatform()
            {
				m_completed = true,
				m_platformCode = platformCode,
				m_specificKey = string.Empty
			});

			return Result(unlockedAction);
		}

		[RMCMethod(9)]
		public RMCResult SetActionsCompleted(IEnumerable<string> actionCodeList, string cultureName, string platformCode)
		{
			var actionList = new List<UplayAction>();
			return Result(actionList);
		}

		[RMCMethod(10)]
		public RMCResult GetUserToken()
		{
            UNIMPLEMENTED();
            return Error(0);
        }

		[RMCMethod(11)]
		public RMCResult GetVirtualCurrencyUserBalance(string platform_code)
		{
            UNIMPLEMENTED();
            return Error(0);
        }

		[RMCMethod(12)]
		public RMCResult GetSectionsByKey(string culture_name, string section_key)
		{
            UNIMPLEMENTED();
            return Error(0);
        }

        [RMCMethod(13)]
        public RMCResult BuyReward(string reward_code, string platform_code)
        {
            UNIMPLEMENTED();
            return Error(0);
        }
    }
}
