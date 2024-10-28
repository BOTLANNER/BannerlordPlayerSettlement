using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BannerlordPlayerSettlement.Utils;

using SandBox.View.Map;

using TaleWorlds.Engine;

namespace BannerlordPlayerSettlement.Extensions
{
    public static class GameEntityExtensions
    {

        public static void ClearEntity(this GameEntity? entity)
        {
            if (entity != null)
            {
                try
                {
                    try
                    {
                        MapScreen.VisualsOfEntities.Remove(entity.Pointer);
                    }
                    catch (Exception e)
                    {
                        LogManager.Log.NotifyBad(e);
                    }
                    foreach (GameEntity child in entity.GetChildren().ToList())
                    {
                        try
                        {
                            MapScreen.VisualsOfEntities.Remove(child.Pointer);
                            child.Remove(112);
                        }
                        catch (Exception e)
                        {
                            LogManager.Log.NotifyBad(e);
                        }
                    }
                    try
                    {
                        entity.ClearEntityComponents(true, true, true);
                        entity.ClearOnlyOwnComponents();
                        entity.ClearComponents();
                    }
                    catch (Exception e)
                    {
                        LogManager.Log.NotifyBad(e);
                    }
                    entity.Remove(112);
                }
                catch (Exception e)
                {
                    LogManager.Log.NotifyBad(e);
                }
            }
        }
    }
}
