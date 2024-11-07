using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using ThirdEye.Util;
using UnityEngine;
using static ThirdEye.ThirdEyePlugin;

namespace ThirdEye.Patches
{
    [HarmonyPatch(typeof(Character), nameof(Character.SetWalk))]
    public static class SetWalkPatch
    {
        //We stop the walk toggle button from doing anything if we use it while crouching.
        //Because in this mod, it is a hotkey to trigger the sonar ping.
        //But we only do this if a custom keybind has not been set.
        public static void Prefix(Character __instance, ref bool walk)
        {
            if (__instance is Player && __instance.IsCrouching() && !KeyBind.Value.IsDown())
            {
                walk = !walk;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static void Postfix(ref Player __instance)
        {
            if (__instance != Player.m_localPlayer || !__instance.TakeInput()) return;
            bool keyPressed = false;

            //We check to see if the custom hotkey is pressed, or if the walk key is pressed while crouched.
            if (KeyBind.Value.IsDown() && !InvalidKey && __instance.IsCrouching())
            {
                try
                {
                    if (KeyBind.Value.IsDown())
                    {
                        keyPressed = true;
                    }
                }
                catch (Exception e)
                {
                    //We only warn about an invalid keybind once. Then we revert to the walk key while crouching.
                    if (!InvalidKey)
                    {
                        InvalidKey = true;
                        ThirdEyeLogger.LogDebug($"ThirdEye ERROR: You bound an invalid key! Please check the config file and try again. You can consult a Unity guide for a list of valid key codes. {e}");
                    }
                }
            }
            else if (__instance.IsCrouching() && ZInput.GetButtonDown("ToggleWalk"))
            {
                keyPressed = true;
            }

            //We only proceed if the key was pressed.
            if (!keyPressed) return;
            //First we check if the player has enough Stamina to use the ping, if not we don't do it.
            if (__instance.HaveStamina(StaminaDrain.Value))
            {
                __instance.UseStamina(StaminaDrain.Value);
            }
            else
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Not enough stamina");
                Hud.instance.StaminaBarEmptyFlash();
                return;
            }

            //Create the ping effects.
            if (ShowVisual.Value == Toggle.On)
            {
                Transform visualObject = UnityEngine.Object.Instantiate(ZNetSceneGrabber.GetVisualEffect(), __instance.GetHeadPoint(), Quaternion.identity);
            }

            if (PlayAudio.Value == Toggle.On)
            {
                GameObject audioObject = UnityEngine.Object.Instantiate(ZNetSceneGrabber.GetAudioEffect(),
                    __instance.GetHeadPoint(), Quaternion.identity);
            }

            //Do the Get Creatures.
            List<Character> guysList = Character.GetAllCharacters();
            EnemyHud.instance.m_refPoint = __instance.transform.position;
            int guysNum = 0;

            //Loop through the characters.
            if (AllowPlayerDetection.Value == Toggle.On)
            {
                foreach (Character character in guysList.Where(character => EnemyHud.instance.TestShow(character, false) 
                                                                            && !(character.IsTamed() && ShowTames.Value == Toggle.Off)))
                {
                    guysNum++;
                    EnemyHud.instance.ShowHud(character, false);
                    EnemyHud.instance.m_huds.TryGetValue(character, out EnemyHud.HudData hud);
                    if (hud == null) continue;
                    hud.m_hoverTimer = 0F;
                    hud.m_gui.SetActive(true);
                }
            }
            else
            {
                foreach (Character character in guysList.Where(character =>
                             character is not Player && EnemyHud.instance.TestShow(character, false) 
                                                     && !(character.IsTamed() && ShowTames.Value == Toggle.Off)))
                {
                    guysNum++;
                    EnemyHud.instance.ShowHud(character, false);
                    EnemyHud.instance.m_huds.TryGetValue(character, out EnemyHud.HudData hud);
                    if (hud == null) continue;
                    hud.m_hoverTimer = 0F;
                    hud.m_gui.SetActive(true);
                }
            }

            //Update the enemy huds list if we found anything
            if (guysNum > 0)
            {
                Sadle? sadle = null;
                EnemyHud.instance.UpdateHuds(__instance, sadle, Time.deltaTime);
            }

            //Show message about how many enemies are nearby.
            if (ShowMessage.Value == Toggle.Off) return;
            if (CustomMessage.Value.Length > 0)
            {
                string newMessage = CustomMessage.Value.Replace("#", guysNum.ToString());
                __instance.Message(MessageHud.MessageType.Center,
                    "<color=" + MessageColor.Value + ">" + newMessage + "</color>");
            }
            else
            {
                __instance.Message(MessageHud.MessageType.Center,
                    "<color=" + MessageColor.Value + ">" + guysNum + " creature" +
                    (guysNum == 1 ? "" : "s") + " found nearby.</color>");
            }
        }
    }
}