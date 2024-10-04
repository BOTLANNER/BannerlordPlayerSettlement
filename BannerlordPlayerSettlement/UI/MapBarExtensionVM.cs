using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

using BannerlordPlayerSettlement.Behaviours;
using BannerlordPlayerSettlement.Saves;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace BannerlordPlayerSettlement.UI
{
    [ViewModelMixin(refreshMethodName: "RefreshValues")]

    public class MapBarExtensionVM : BaseViewModelMixin<MapBarVM>
    {
        public static MapBarExtensionVM? Current = null;

        //[DataSourceProperty]
        //public WeakReference<MapBarExtensionVM> Mixin => new(this);

        private bool _isCreatePlayerSettlementEnabled = true;
        private bool _isCreatePlayerSettlementVisible = true;

        private bool forceHide = false;

        private HintViewModel? _disableReasonHint;

        public MapBarExtensionVM(MapBarVM vm) : base(vm)
        {
            Current = this;
            CalculateEnabled();
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
                    ViewModel?.OnPropertyChangedWithValue(value, "IsCreatePlayerSettlementAllowed");
                }
            }
        }
        [DataSourceProperty]
        public bool IsCreatePlayerSettlementVisible
        {
            get
            {
                return this._isCreatePlayerSettlementVisible && !forceHide;
            }
            set
            {
                if (value != this._isCreatePlayerSettlementVisible)
                {
                    this._isCreatePlayerSettlementVisible = value;
                    ViewModel?.OnPropertyChangedWithValue(value, "IsCreatePlayerSettlementVisible");
                }
            }
        }

        [DataSourceProperty]
        public string CreatePlayerSettlementText
        {
            get
            {
                return NextBuildType == SettlementType.Village ?
                    new TextObject("{=player_settlement_13}Build a Village").ToString() :
                        NextBuildType == SettlementType.Castle ?
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
                    ViewModel?.OnPropertyChangedWithValue(value, "DisableHint");
                }
            }
        }

        private SettlementType NextBuildType = SettlementType.None;
        private PlayerSettlementItem? BoundTarget = null;

        public void Tick(float dt)
        {
            if (ViewModel?.MapTimeControl != null)
            {
                if (forceHide == ViewModel.MapTimeControl.IsCenterPanelEnabled)
                {
                    OnRefresh();

                    forceHide = !ViewModel.MapTimeControl.IsCenterPanelEnabled;
                    ViewModel.OnPropertyChangedWithValue(IsCreatePlayerSettlementVisible, "IsCreatePlayerSettlementVisible");
                }
            }
        }

        private void CalculateEnabled()
        {
            TextObject? disableReason = null;
            NextBuildType = SettlementType.None;

            if (Main.Settings == null || PlayerSettlementInfo.Instance == null || PlayerSettlementBehaviour.Instance == null)
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


            if (PlayerSettlementBehaviour.Instance!.ReachedMax || PlayerSettlementBehaviour.Instance!.HasRequest)
            {
                // Either reached max or about to create something
                disableReason ??= new TextObject("<Already Created or About to Create>");
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
                    (PlayerSettlementInfo.Instance.Towns.Any(t => t.BuildEnd.IsFuture || t.Villages.Any(v => v.BuildEnd.IsFuture)) ||
                     PlayerSettlementInfo.Instance.Castles.Any(c => c.BuildEnd.IsFuture || c.Villages.Any(v => v.BuildEnd.IsFuture))))
                {
                    disableReason ??= new TextObject("{=player_settlement_h_06}Construction in progress");
                }

                NextBuildType = PlayerSettlementBehaviour.Instance.GetNextBuildType(out BoundTarget);
                ViewModel?.OnPropertyChangedWithValue(CreatePlayerSettlementText, "CreatePlayerSettlementText");

                if (NextBuildType == SettlementType.Village && Main.Settings.RequireVillageGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredVillageGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredVillageGold);
                    }
                }
                else if (NextBuildType == SettlementType.Town && Main.Settings.RequireGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredGold);
                    }
                }
                else if (NextBuildType == SettlementType.Castle && Main.Settings.RequireCastleGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredCastleGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredCastleGold);
                    }
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

        [DataSourceMethod]
        public void ExecuteCreatePlayerSettlement()
        {
            //PlayerSettlementItem? boundTarget = null;
            //NextBuildType = PlayerSettlementBehaviour.Instance?.GetNextBuildType(out boundTarget) ?? SettlementType.None;
            if (NextBuildType == SettlementType.None)
            {
                return;
            }

            var bound = BoundTarget?.Settlement;

            var confirm =
                NextBuildType == SettlementType.Village ?
                new TextObject("{=player_settlement_14}Are you sure you want to build your village here?") :
                    NextBuildType == SettlementType.Castle ?
                    new TextObject("{=player_settlement_18}Are you sure you want to build your castle here?") :
                        // buildType == SettlementType.Town
                        new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");

            InformationManager.ShowInquiry(new InquiryData(CreatePlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                () =>
                {
                    if (PlayerSettlementBehaviour.Instance != null)
                    {
                        PlayerSettlementBehaviour.Instance.SettlementRequest = NextBuildType;
                        PlayerSettlementBehaviour.Instance.RequestBoundSettlement = bound;

                        IsCreatePlayerSettlementAllowed = false;
                        IsCreatePlayerSettlementVisible = false;
                        NextBuildType = SettlementType.None;
                        OnRefresh();


                        Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppablePlay;
                    }
                },
                () =>
                {
                    // Cancelled. Do nothing.
                    InformationManager.HideInquiry();
                }), true, false);
        }


        public override void OnRefresh()
        {
            base.OnRefresh();

            CalculateEnabled();
        }

        public override void OnFinalize()
        {
            base.OnFinalize();

            Current = null;
        }
    }
}
