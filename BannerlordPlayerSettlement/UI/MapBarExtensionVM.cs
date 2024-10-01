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

        [DataSourceProperty]
        public WeakReference<MapBarExtensionVM> Mixin => new(this);

        private bool _isCreatePlayerSettlementEnabled = true;
        private bool _isCreatePlayerSettlementVisible = true;

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
                return this._isCreatePlayerSettlementVisible;
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
                return new TextObject("{=player_settlement_04}Build a Town").ToString();
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

        private void CalculateEnabled()
        {
            TextObject? disableReason = null;

            if (PlayerSettlementInfo.Instance?.PlayerSettlement != null || (PlayerSettlementBehaviour.Instance?.CreateSettlement ?? false))
            {
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

                if (Main.Settings.RequireGold)
                {
                    if ((Hero.MainHero?.Gold ?? 0) < Main.Settings.RequiredGold)
                    {
                        disableReason ??= new TextObject("{=player_settlement_h_05}Not enough funds ({CURRENT_FUNDS}/{REQUIRED_FUNDS})");
                        disableReason.SetTextVariable("CURRENT_FUNDS", Hero.MainHero?.Gold ?? 0);
                        disableReason.SetTextVariable("REQUIRED_FUNDS", Main.Settings.RequiredGold);
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
            var confirm = new TextObject("{=player_settlement_05}Are you sure you want to build your town here?");
            InformationManager.ShowInquiry(new InquiryData(CreatePlayerSettlementText, confirm.ToString(), true, true, GameTexts.FindText("str_ok", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(),
                () =>
                {
                    if (PlayerSettlementBehaviour.Instance != null)
                    {
                        PlayerSettlementBehaviour.Instance.CreateSettlement = true;

                        IsCreatePlayerSettlementAllowed = false;
                        IsCreatePlayerSettlementVisible = false;
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
