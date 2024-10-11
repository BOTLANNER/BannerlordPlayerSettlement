using System;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map.MapBar;

namespace BannerlordPlayerSettlement.UI.Widgets
{
    public class PlayerSettlementInfoWidget : Widget
    {
        private ButtonWidget _extendButtonWidget;

        private bool _isInfoBarExtended;

        [Editor(false)]
        public ButtonWidget ExtendButtonWidget
        {
            get
            {
                return this._extendButtonWidget;
            }
            set
            {
                if (this._extendButtonWidget != value)
                {
                    this._extendButtonWidget = value;
                    base.OnPropertyChanged<ButtonWidget>(value, "ExtendButtonWidget");
                    if (!this._extendButtonWidget.ClickEventHandlers.Contains(new Action<Widget>(this.OnExtendButtonClick)))
                    {
                        this._extendButtonWidget.ClickEventHandlers.Add(new Action<Widget>(this.OnExtendButtonClick));
                    }
                    this.RefreshVerticalVisual();
                }
            }
        }

        [Editor(false)]
        public bool IsInfoBarExtended
        {
            get
            {
                return this._isInfoBarExtended;
            }
            set
            {
                if (this._isInfoBarExtended != value)
                {
                    this._isInfoBarExtended = value;
                    base.OnPropertyChanged(value, "IsInfoBarExtended");
                    MapInfoBarWidget.MapBarExtendStateChangeEvent mapBarExtendStateChangeEvent = this.OnMapInfoBarExtendStateChange;
                    if (mapBarExtendStateChangeEvent == null)
                    {
                        return;
                    }
                    mapBarExtendStateChangeEvent(this.IsInfoBarExtended);
                }
            }
        }

        public PlayerSettlementInfoWidget(UIContext context) : base(context)
        {
            base.AddState("Disabled");
        }

        private void OnExtendButtonClick(Widget widget)
        {
            this.IsInfoBarExtended = !this.IsInfoBarExtended;
            this.RefreshBarExtendState();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            this.RefreshBarExtendState();
        }

        private void RefreshBarExtendState()
        {
            if (this.IsInfoBarExtended && base.CurrentState != "Extended")
            {
                this.SetState("Extended");
                this.RefreshVerticalVisual();
                return;
            }
            if (!this.IsInfoBarExtended && base.CurrentState != "Default")
            {
                this.SetState("Default");
                this.RefreshVerticalVisual();
            }
        }

        private void RefreshVerticalVisual()
        {
            foreach (Style style in this.ExtendButtonWidget.Brush.Styles)
            {
                for (int i = 0; i < style.LayerCount; i++)
                {
                    style.GetLayer(i).VerticalFlip = !this.IsInfoBarExtended;
                }
            }
        }

        public event MapInfoBarWidget.MapBarExtendStateChangeEvent OnMapInfoBarExtendStateChange;

        public delegate void MapBarExtendStateChangeEvent(bool newState);
    }
}
