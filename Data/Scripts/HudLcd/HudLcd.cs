using System;
using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;

using Draygo.API;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

using Jawastew;
using System.Text;

namespace Jawastew.HudLcd
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false)]
    sealed class HudLcd : MyGameLogicComponent
    {
        //Workshop & Debug
        const long WORKSHOP_ID = 911144486;
        const string MOD = "HudLcd";
        static bool debug = false;
        bool debugTrigger = debug;

        //Static
        public static bool IsInit => textAPI != null;
        public static bool IsAPIAlive => textAPI != null && textAPI.Heartbeat;
        static bool isServer = false;

        static HudAPIv2 textAPI = null;

        // Defaults &  Config Format
        const string configTag = "hudlcd";
        const char configDelim = ':';
        const double textPosXDefault = -0.98;
        const double textPosYDefault = -0.2;
        const double textScaleDefault = 0.8;
        const string textFontDefault = "white";
        const bool textFontShadowDefault = false;

        // Textpanel Current
        IMyTextPanel thisTextPanel = null;
        HudAPIv2.HUDMessage thisHUDMessage;
        Vector2D thisTextPosition = new Vector2D(textPosXDefault, textPosYDefault);
        double thisTextScale = textScaleDefault;
        string thisTextcolour = null;
        string thisTextFont = textFontDefault;
        bool thisTextFontShadow = textFontShadowDefault;

        IMyTerminalBlock ControlledEntity => MyAPIGateway.Session?.LocalHumanPlayer?.Controller?.ControlledEntity as IMyTerminalBlock;
        bool IsControlled => ControlledEntity != null && ControlledEntity.CubeGrid == thisTextPanel.CubeGrid;

        // Cache
        string configCache = null;
        double textScaleCache = textScaleDefault;
        string textFontCache = textFontDefault;
        StringBuilder thisMessage = null;


        //Initialises textAPI once for clients, checks if is Server
        static void Initialize()
        {
            if (IsInit) return;
            if ((MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated))
            {
                isServer = true;
                return;
            }
            textAPI = new HudAPIv2();
        }


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            thisTextPanel = Entity as IMyTextPanel;
            configCache = null; // reset cache
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!IsInit) Initialize();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME; //If you change the update interval, also change the int ttl accordingly for the HUDMessage
        }

        //for Performance every 10th frame
        public override void UpdateBeforeSimulation10()
        {
            if (isServer) return;
            if (!IsAPIAlive) return;
            if (debugTrigger)
            {
                if (debug) MyAPIGateway.Utilities.ShowMessage("HudLcd", "New TextPanel" + thisTextPanel.GetPublicTitle());
                debugTrigger = false;
            }
            if (IsControlled && UpdateValues())
                UpdateHUDMessage();
            else
                PurgeHUDMessage();
        }


        /*  UpdateValues()
         *  returns true when a valid configuration for hudlcd has been found (and set).
         *  returns false otherwise.
         */
        private bool UpdateValues()
        {
            // try and fetch new configuration
            string config;
            if (thisTextPanel.GetPublicTitle() != null && thisTextPanel.GetPublicTitle().ToLower().Contains(configTag))
            {
                config = thisTextPanel.GetPublicTitle();
            }
            else if (thisTextPanel.CustomData != null && thisTextPanel.CustomData.ToLower().Contains(configTag))
            {
                config = thisTextPanel.CustomData;
            }
            else
            {
                // no hudlcd config found.
                return false;
            }
            
            thisTextScale = thisTextPanel.FontSize;

            switch (thisTextPanel.Font)
            {
                case "Monospace":
                    thisTextFont = "monospace";
                    break;
                default:
                    thisTextFont = textFontDefault;
                    break;
            }

            // purge HUDMessage, if change detected
            if (config != configCache || textScaleCache != thisTextScale || textFontCache != thisTextFont)
            {
                if (debug) MyAPIGateway.Utilities.ShowMessage("HudLcd", "Detected Config Change: " + config + thisTextPanel.Font);
                PurgeHUDMessage();
            }

            // Update Cache
            configCache = config;
            textScaleCache = thisTextScale;
            textFontCache = thisTextFont;

            // Get config from config string
            String[] lines = config.Split('\n');
            foreach (String line in lines)
            {
                if (line.ToLower().Contains(configTag))
                {
                    if (debug && !config.Equals(configCache)) MyAPIGateway.Utilities.ShowMessage("HudLcd", "Detected Config Change: " + line.Substring(line.IndexOf(configTag)));
                    String[] rawconf = line.Substring(line.IndexOf(configTag)).Split(configDelim); // remove everything before hudlcd in the string.
                    for (int i = 0; i < 6; i++)
                    {
                        if (rawconf.Length > i && rawconf[i].Trim() != "") // Set values from Config Line
                        {
                            switch (i)
                            {
                                case 0:
                                    break;
                                case 1:
                                    thisTextPosition.X = trygetdouble(rawconf[i], textPosXDefault);
                                    break;
                                case 2:
                                    thisTextPosition.Y = trygetdouble(rawconf[i], textPosYDefault);
                                    break;
                                case 3:
                                    thisTextScale = trygetdouble(rawconf[i], textScaleDefault);
                                    break;
                                case 4:
                                    thisTextcolour = "<color=" + rawconf[i].Trim() + ">";
                                    break;
                                case 5:
                                    if (rawconf[i].Trim() == "1")
                                    {
                                        thisTextFontShadow = true;
                                    }
                                    else
                                    {
                                        thisTextFontShadow = false;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        else // Set Default Values
                        {
                            switch (i)
                            {
                                case 0:
                                    break;
                                case 1:
                                    thisTextPosition.X = textPosXDefault;
                                    break;
                                case 2:
                                    thisTextPosition.Y = textPosYDefault;
                                    break;
                                case 3:
                                    thisTextScale = thisTextPanel.FontSize;
                                    break;
                                case 4:
                                    var fontColor = thisTextPanel.GetValueColor("FontColor");
                                    thisTextcolour = $"<color={fontColor.R},{fontColor.G},{fontColor.B}>";
                                    break;
                                case 5:
                                    thisTextFontShadow = false;
                                    break;
                                default:
                                    break;
                            }
                        }

                    }
                    break; // stop processing lines from Custom Data
                }

            }
            return true;

        }

        private double trygetdouble(string v, double defaultval)
        {
            try
            {
                return double.Parse(v);
            }
            catch (Exception)
            {
                return defaultval;
            }
        }

        private void UpdateHUDMessage()
        {
            if (thisMessage == null)
            {
                thisMessage = new StringBuilder(500);
            }
            thisMessage.Clear();
            thisMessage.Append(thisTextcolour);
            thisTextPanel.ReadText(thisMessage, true);
            if (thisHUDMessage == null)
            {
                if (debug) MyAPIGateway.Utilities.ShowMessage("HudLcd", "Drawing hud with: " + thisMessage);
                thisHUDMessage = new HudAPIv2.HUDMessage(
                    Message: thisMessage,
                    Origin: thisTextPosition, 
                    Offset: null,
                    TimeToLive: -1,
                    Scale: thisTextScale,
                    HideHud: false,
                    Shadowing: thisTextFontShadow,
                    ShadowColor: Color.Black,
                    Blend: BlendTypeEnum.PostPP,
                    Font: thisTextFont
                    );
            }
        }

        private void PurgeHUDMessage()
        {
            if (thisHUDMessage != null)
            {
                thisHUDMessage.Visible = false;
                thisHUDMessage.DeleteMessage();
                thisHUDMessage = null;
            }
        }

        public override void Close()
        {
            PurgeHUDMessage();
        }

    }

    // [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LCDPanelsBlock), false)]

}


