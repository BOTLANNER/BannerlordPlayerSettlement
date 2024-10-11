
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace BannerlordPlayerSettlement.UI.Viewmodels
{
    [ViewModelMixin(refreshMethodName: "RefreshValues")]

    public class MapBarExtensionVM : BaseViewModelMixin<MapBarVM>
    {
        public static MapBarExtensionVM? Current = null;

        private PlayerSettlementInfoVM? _playerSettlementInfo;

        public MapBarExtensionVM(MapBarVM vm) : base(vm)
        {
            Current = this;

            _playerSettlementInfo = new PlayerSettlementInfoVM(this, vm);
        }

        [DataSourceProperty]
        public PlayerSettlementInfoVM? PlayerSettlementInfo
        {
            get
            {
                return this._playerSettlementInfo;
            }
            set
            {
                if (value != this._playerSettlementInfo)
                {
                    this._playerSettlementInfo = value;
                    ViewModel?.OnPropertyChangedWithValue<PlayerSettlementInfoVM>(value, "PlayerSettlementInfo");
                }
            }
        }

        public void Tick(float dt)
        {
            this._playerSettlementInfo?.Tick(dt);
        }

        public override void OnRefresh()
        {
            base.OnRefresh();

            _playerSettlementInfo?.RefreshValues();
        }

        public override void OnFinalize()
        {
            base.OnFinalize();

            this._playerSettlementInfo = null;

            Current = null;
        }
    }
}
