using System.Linq;

using Bannerlord.UIExtenderEx.Attributes;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Saves;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace BannerlordPlayerSettlement.UI.Viewmodels
{
    public class PlayerSettlementBuildVM : ViewModel
    {
        private readonly PlayerSettlementInfoVM? owner;
        private readonly SettlementType settlementType = SettlementType.None;

        private bool _isCreatePlayerSettlementEnabled = true;
        private bool _isCreatePlayerSettlementVisible = true;

        private HintViewModel? _disableReasonHint;

        public PlayerSettlementBuildVM() { }
        public PlayerSettlementBuildVM(PlayerSettlementInfoVM owner, SettlementType settlementType)
        {
            this.owner = owner;
            this.settlementType = settlementType;
        }


        [DataSourceProperty]
        public bool IsCreatePlayerSettlementAllowed
        {
            get
            {
                return this._isCreatePlayerSettlementEnabled && this._isCreatePlayerSettlementVisible;
            }
            set
            {
                if (value != this._isCreatePlayerSettlementEnabled)
                {
                    this._isCreatePlayerSettlementEnabled = value;
                    base.OnPropertyChangedWithValue(value, nameof(IsCreatePlayerSettlementAllowed));
                }
            }
        }
        [DataSourceProperty]
        public bool IsCreatePlayerSettlementVisible
        {
            get
            {
                return this._isCreatePlayerSettlementVisible;
            }
            set
            {
                if (value != this._isCreatePlayerSettlementVisible)
                {
                    this._isCreatePlayerSettlementVisible = value;
                    base.OnPropertyChangedWithValue(value, nameof(IsCreatePlayerSettlementVisible));
                }
            }
        }

        [DataSourceProperty]
        public string CreatePlayerSettlementText
        {
            get
            {
                return settlementType == SettlementType.Village ?
                    new TextObject("{=player_settlement_13}Build a Village").ToString() :
                        settlementType == SettlementType.Castle ?
                        new TextObject("{=player_settlement_19}Build a Castle").ToString() :
                            //NextBuildType == SettlementType.Town ?
                            new TextObject("{=player_settlement_04}Build a Town").ToString();
            }
        }

        [DataSourceProperty]
        public HintViewModel? DisableHint
        {
            get
            {
                return this._disableReasonHint;
            }
            set
            {
                if (value != this._disableReasonHint)
                {
                    this._disableReasonHint = value;
                    base.OnPropertyChangedWithValue(value, nameof(DisableHint));
                }
            }
        }

        [DataSourceMethod]
        public void ExecuteCreatePlayerSettlement()
        {
            if (settlementType == SettlementType.None)
            {
                return;
            }

            if (PlayerSettlementBehaviour.Instance != null)
            {
                PlayerSettlementBehaviour.Instance.SettlementRequest = settlementType;

                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;

                owner?.RefreshValues();

                Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppablePlay;
            }
        }


        internal void CalculateEnabled()
        {
            TextObject? disableReason = null;

            if (Main.Settings == null || PlayerSettlementInfo.Instance == null || PlayerSettlementBehaviour.Instance == null /*|| Settlement.CurrentSettlement != null*/)
            {
                disableReason = new TextObject();
                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;
                this.DisableHint = new HintViewModel(disableReason, null);
                return;
            }

            if (PlayerSettlementInfo.Instance.Towns == null)
            {
                PlayerSettlementInfo.Instance.Towns = new();
            }
            if (PlayerSettlementInfo.Instance.Castles == null)
            {
                PlayerSettlementInfo.Instance.Castles = new();
            }
            if (PlayerSettlementInfo.Instance.PlayerVillages == null)
            {
                PlayerSettlementInfo.Instance.PlayerVillages = new();
            }

            if (settlementType == SettlementType.Town && PlayerSettlementInfo.Instance.Towns.Count >= Main.Settings.MaxTowns)
            {
                disableReason ??= new TextObject("{=player_settlement_h_08}Maximum number of towns have been built");
                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;
            }
            else if (settlementType == SettlementType.Castle && PlayerSettlementInfo.Instance.Castles.Count >= Main.Settings.MaxCastles)
            {
                disableReason ??= new TextObject("{=player_settlement_h_09}Maximum number of castles have been built");
                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;
            }
            else if (settlementType == SettlementType.Village && PlayerSettlementInfo.Instance.TotalVillages >= Settings.HardMaxVillages)
            {
                disableReason ??= new TextObject("{=player_settlement_h_10}Maximum number of villages have been built");
                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;
            }
            else if (PlayerSettlementBehaviour.Instance!.ReachedMax || PlayerSettlementBehaviour.Instance!.HasRequest || PlayerSettlementBehaviour.Instance!.IsPlacingSettlement || PlayerSettlementBehaviour.Instance!.IsPlacingGate)
            {
                // Either reached max or about to create something
                disableReason ??= new TextObject(" - ");
                IsCreatePlayerSettlementAllowed = false;
                IsCreatePlayerSettlementVisible = false;
            }
            else
            {
                IsCreatePlayerSettlementVisible = true;
            }

            if (Main.Settings != null && Main.Settings.Enabled)
            {
                if (Game.Current == null || Campaign.Current == null || Hero.MainHero == null)
                {
                    disableReason ??= new TextObject("{=player_settlement_h_02}Not in an active game!");
                }

                if (Main.Settings.RequireClanTier)
                {
                    if ((Hero.MainHero?.Clan?.Tier ?? 0) < Main.Settings.RequiredClanTier)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_03}Clan tier too low. {TIER} required");
                        disableReason.SetTextVariable("TIER", Main.Settings.RequiredClanTier);
                    }
                }

                if (Main.Settings.SingleConstruction &&
                    (PlayerSettlementInfo.Instance.PlayerVillages.Any(t => t.BuildEnd.IsFuture) ||
                     PlayerSettlementInfo.Instance.Towns.Any(t => t.BuildEnd.IsFuture || t.Villages.Any(v => v.BuildEnd.IsFuture)) ||
                     PlayerSettlementInfo.Instance.Castles.Any(c => c.BuildEnd.IsFuture || c.Villages.Any(v => v.BuildEnd.IsFuture))))
                {
                    disableReason ??= new TextObject("{=player_settlement_h_06}Construction in progress");
                }


                if (settlementType == SettlementType.Village && Main.Settings.RequireVillageGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredVillageGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredVillageGold);
                    }
                }
                else if (settlementType == SettlementType.Town && Main.Settings.RequireGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredGold);
                    }
                }
                else if (settlementType == SettlementType.Castle && Main.Settings.RequireCastleGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredCastleGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredCastleGold);
                    }
                }

                if (settlementType == SettlementType.Village && PlayerSettlementBehaviour.Instance.GetPotentialVillageBoundOwners().Count() == 0)
                {
                    disableReason ??= new TextObject("{=player_settlement_h_11}No candidate for village to be bound to");
                }
            }
            else
            {
                disableReason = new TextObject("{=player_settlement_h_04}Player Settlement not enabled");
            }

            if (disableReason == null)
            {
                disableReason = TextObject.Empty;
                IsCreatePlayerSettlementAllowed = true;
            }
            else
            {
                IsCreatePlayerSettlementAllowed = false;
                if (Main.Settings?.HideButtonUntilReady ?? true)
                {
                    IsCreatePlayerSettlementVisible = false;
                }
            }


            if (!IsCreatePlayerSettlementAllowed)
            {
                this.DisableHint = new HintViewModel(disableReason, null);
            }
            else
            {
                this.DisableHint = null;
                IsCreatePlayerSettlementVisible = true;
            }
        }

    }
}
