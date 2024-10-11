using BannerlordPlayerSettlement.Saves;

using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace BannerlordPlayerSettlement.UI.Viewmodels
{
    public class PlayerSettlementInfoVM : MapInfoVM
    {
        private readonly MapBarExtensionVM? mapBarExtensionVM;
        private readonly MapBarVM? mapBarVM;

        private PlayerSettlementBuildVM? _playerTownBuildInfo;
        private PlayerSettlementBuildVM? _playerVillageBuildInfo;
        private PlayerSettlementBuildVM? _playerCastleBuildInfo;

        private HintViewModel? _disableReasonHint;

        private bool forceHide = false;

        public PlayerSettlementInfoVM() { }
        public PlayerSettlementInfoVM(MapBarExtensionVM mapBarExtensionVM, MapBarVM mapBarVM) // : base()
        {
            this.mapBarExtensionVM = mapBarExtensionVM;
            this.mapBarVM = mapBarVM;
            _playerTownBuildInfo = new PlayerSettlementBuildVM(this, SettlementType.Town);
            _playerVillageBuildInfo = new PlayerSettlementBuildVM(this, SettlementType.Village);
            _playerCastleBuildInfo = new PlayerSettlementBuildVM(this, SettlementType.Castle);

            ExtendHint = new HintViewModel(new TextObject("{=player_settlement_h_12}Show/Hide player settlement build options"));

            CalculateEnabled();
        }

        [DataSourceProperty]
        public PlayerSettlementBuildVM? PlayerTownBuildInfo
        {
            get
            {
                return this._playerTownBuildInfo;
            }
            set
            {
                if (value != this._playerTownBuildInfo)
                {
                    this._playerTownBuildInfo = value;
                    base.OnPropertyChangedWithValue<PlayerSettlementBuildVM>(value!, nameof(PlayerTownBuildInfo));
                }
            }
        }

        [DataSourceProperty]
        public PlayerSettlementBuildVM? PlayerVillageBuildInfo
        {
            get
            {
                return this._playerVillageBuildInfo;
            }
            set
            {
                if (value != this._playerVillageBuildInfo)
                {
                    this._playerVillageBuildInfo = value;
                    base.OnPropertyChangedWithValue<PlayerSettlementBuildVM>(value!, nameof(PlayerVillageBuildInfo));
                }
            }
        }

        [DataSourceProperty]
        public PlayerSettlementBuildVM? PlayerCastleBuildInfo
        {
            get
            {
                return this._playerCastleBuildInfo;
            }
            set
            {
                if (value != this._playerCastleBuildInfo)
                {
                    this._playerCastleBuildInfo = value;
                    base.OnPropertyChangedWithValue<PlayerSettlementBuildVM>(value!, nameof(PlayerCastleBuildInfo));
                }
            }
        }

        [DataSourceProperty]
        public bool IsOverallAllowed
        {
            get
            {
                return
                    (this.PlayerCastleBuildInfo?.IsCreatePlayerSettlementAllowed ?? false) ||
                    (this.PlayerTownBuildInfo?.IsCreatePlayerSettlementAllowed ?? false) ||
                    (this.PlayerVillageBuildInfo?.IsCreatePlayerSettlementAllowed ?? false);
            }
        }

        [DataSourceProperty]
        public bool IsOverallVisible
        {
            get
            {
                return Main.Settings != null &&
                      !Main.Settings.ImmersiveMode && 
                      !forceHide &&
                     ((this.PlayerCastleBuildInfo?.IsCreatePlayerSettlementVisible ?? false) ||
                     (this.PlayerTownBuildInfo?.IsCreatePlayerSettlementVisible ?? false) ||
                     (this.PlayerVillageBuildInfo?.IsCreatePlayerSettlementVisible ?? false));
            }
        }

        [DataSourceProperty]
        public HintViewModel? DisableHint
        {
            get
            {
                if (!IsOverallAllowed)
                {
                    //if (_disableReasonHint == null)
                    {
                        var textObject = new TextObject("{=player_settlement_h_07}Cannot build town: {TOWN_REASON}\r\n\r\nCannot build castle: {CASTLE_REASON}\r\n\r\nCannot build village: {VILLAGE_REASON}");
                        textObject.SetTextVariable("CASTLE_REASON", _playerCastleBuildInfo?.DisableHint?.HintText?.ToString() ?? " - ");
                        textObject.SetTextVariable("TOWN_REASON", _playerTownBuildInfo?.DisableHint?.HintText?.ToString() ?? " - ");
                        textObject.SetTextVariable("VILLAGE_REASON", _playerVillageBuildInfo?.DisableHint?.HintText?.ToString() ?? " - ");
                        _disableReasonHint = new HintViewModel(textObject);
                    }
                    return _disableReasonHint;
                }
                _disableReasonHint = null;
                return _disableReasonHint;
            }
        }

        public void Tick(float dt)
        {
            if (mapBarVM?.MapTimeControl != null)
            {
                var shouldHide = !mapBarVM.MapTimeControl.IsCenterPanelEnabled || Settlement.CurrentSettlement != null;
                if (forceHide != shouldHide)
                {
                    RefreshValues();

                    forceHide = shouldHide;
                    base.OnPropertyChangedWithValue(IsOverallVisible, nameof(IsOverallVisible));
                }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            CalculateEnabled();
        }

        public override void OnFinalize()
        {
            base.OnFinalize();

            this._playerCastleBuildInfo = null;
            this._playerTownBuildInfo = null;
            this._playerVillageBuildInfo = null;
        }


        private void CalculateEnabled()
        {
            _playerTownBuildInfo?.CalculateEnabled();
            _playerCastleBuildInfo?.CalculateEnabled();
            _playerVillageBuildInfo?.CalculateEnabled();


            base.OnPropertyChangedWithValue(IsOverallVisible, nameof(IsOverallVisible));
            base.OnPropertyChangedWithValue(DisableHint, nameof(DisableHint));
            base.OnPropertyChangedWithValue(IsOverallAllowed, nameof(IsOverallAllowed));

            if (!IsOverallAllowed)
            {
                IsInfoBarExtended = false;
            }
        }

    }
}
