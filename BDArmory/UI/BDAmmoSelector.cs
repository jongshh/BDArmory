using System.Collections;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.Modules;
using UnityEngine;
using KSP.Localization;
using System.Collections.Generic;
using BDArmory.Bullets;
using System;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class BDAmmoSelector : MonoBehaviour
    {
        public static BDAmmoSelector Instance;
        private Rect windowRect = new Rect(350, 100, 350, 20);

        const float width = 350;
        const float margin = 5;
        const float buttonHeight = 20;

        private bool open = false;
        private bool save = false;
        private float height = 20;

        private string beltString = String.Empty;
        private string GUIstring = String.Empty;
        private string lastGUIstring = String.Empty;
        string countString = String.Empty;
        private int roundCounter = 0;
        int labelLines = 1;

        private Vector2 windowLocation;
        private ModuleWeapon selectedWeapon;

        public string SelectedAmmoType; //presumably Aubranium can use this to filter allowed/banned ammotypes

        public List<string> AList = new List<string>();
        public List<string> ammoDesc = new List<string>();
        private BulletInfo bulletInfo;
        public string guiAmmoTypeString = Localizer.Format("#LOC_BDArmory_Ammo_Slug");

        public void Open(ModuleWeapon weapon, Vector2 position)
        {
            open = true;
            selectedWeapon = weapon;
            windowLocation = position;
            AList = BDAcTools.ParseNames(weapon.bulletType);
            
            for (int a = 0; a < AList.Count; a++)
            {
                bulletInfo = BulletInfo.bullets[AList[a].ToString()];
                guiAmmoTypeString = "";
                if (bulletInfo.subProjectileCount >= 2)
                {
                    guiAmmoTypeString = Localizer.Format("#LOC_BDArmory_Ammo_Shot") + " ";
                }
                if (bulletInfo.apBulletMod >= 1.1)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_AP") + " ";
                }
                if (bulletInfo.apBulletMod < 1.1 && bulletInfo.apBulletMod > 0.8f)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_SAP") + " ";
                }
                if (bulletInfo.explosive)
                {
                    if (bulletInfo.fuzeType.ToLower() != "none")
                    {
                        guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Flak") + " ";
                    }
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Explosive") + " ";
                }
                if (bulletInfo.incendiary)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Incendiary") + " ";
                }
                if (!bulletInfo.explosive && bulletInfo.apBulletMod <= 0.8)
                {
                    guiAmmoTypeString += Localizer.Format("#LOC_BDArmory_Ammo_Slug");
                }
                ammoDesc.Add(guiAmmoTypeString);
            }
           
        }
        protected virtual void OnGUI()
        {
            if (save)
            {
                save = false;
                List<Part>.Enumerator craftPart = EditorLogic.fetch.ship.parts.GetEnumerator();
                while (craftPart.MoveNext())
                {
                    if (craftPart.Current == null) continue;
                    if (craftPart.Current.name != selectedWeapon.name) continue;
                    List<ModuleWeapon>.Enumerator weapon = craftPart.Current.FindModulesImplementing<ModuleWeapon>().GetEnumerator();
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == null) continue;
                        weapon.Current.ammoBelt = beltString;
                        if (!string.IsNullOrEmpty(beltString))
                        {
                            weapon.Current.useCustomBelt = true;
                        }
                        else
                        {
                            weapon.Current.useCustomBelt = false;
                        }
                    }
                    weapon.Dispose();
                }
                craftPart.Dispose();
            }
            if (open)
            {
                windowRect = GUI.Window(this.GetInstanceID(), windowRect, AmmoSelectorWindow, "", BDArmorySetup.BDGuiSkin.window);
            }
            PreventClickThrough();
        }
        private void AmmoSelectorWindow(int id)
        {
            float line = 0.5f;
            string labelString = GUIstring.ToString() + countString.ToString();
            GUIStyle labelStyle = BDArmorySetup.BDGuiSkin.label;
            GUI.Label(new Rect(margin, 0.5f * buttonHeight, width - 2 * margin, buttonHeight), Localizer.Format("#LOC_BDArmory_Ammo_Setup"), labelStyle);
            if (GUI.Button(new Rect(width - 18, 2, 16, 16), "X"))
            {
                open = false;
            }
            line ++;
            GUI.Label(new Rect(margin, line * buttonHeight, width - 2 * margin, buttonHeight), Localizer.Format("#LOC_BDArmory_Ammo_Belt"), labelStyle);
            line += 1.2f;
            GUI.Label(new Rect(margin, line * buttonHeight, width - 2 * margin, buttonHeight*labelLines), labelString, labelStyle);
            line++;

            labelLines = Mathf.CeilToInt((GUIstring.Length / 50));
            float ammolines = 0.1f;
            for (int i = 0; i < AList.Count; i++)
            {
                string ammoname = AList[i];
                if (GUI.Button(new Rect(margin * 2, (line + labelLines + ammolines) * buttonHeight, (width - 4 * margin), buttonHeight), ammoname, BDArmorySetup.BDGuiSkin.button))
                {
                    beltString += ammoname;
                    beltString += "; ";
                    if (lastGUIstring != ammoname)
                    {
                        GUIstring += countString.ToString();
                        GUIstring += ammoname;
                        lastGUIstring = ammoname;
                        roundCounter = 1;
                        countString = "; ";
                    }
                    else
                    {
                        roundCounter++;
                        countString = " X" + roundCounter + "; ";
                    }
                }
                ammolines++;
                if (ammoDesc[i] != null)
                {
                    GUI.Label(new Rect(margin * 4, (line + labelLines + ammolines) * buttonHeight, (width - 8 * margin), buttonHeight), ammoDesc[i], labelStyle);
                    ammolines += 1.1f;
                }
            }
            if (GUI.Button(new Rect(margin * 5, (line + labelLines + ammolines) * buttonHeight, (width - (10 * margin))/2, buttonHeight), Localizer.Format("#LOC_BDArmory_reset")))
            {
                beltString = String.Empty;
                GUIstring = String.Empty;
                countString = String.Empty;
                labelLines = 1;
            }
            if (GUI.Button(new Rect(((margin * 5) + ((width - (10 * margin)) / 2)), (line + labelLines + ammolines) * buttonHeight, (width - (10 * margin)) / 2, buttonHeight), Localizer.Format("#LOC_BDArmory_save")))
            {
                save = true;
                open = false;
            }
            line +=1.5f;
            height = Mathf.Lerp(height, (line + labelLines + ammolines) * buttonHeight, 0.15f);
            windowRect.height = height;
            GUI.DragWindow();
            BDGUIUtils.RepositionWindow(ref windowRect);
        }

        private void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
            windowRect = new Rect( (Screen.width/2) - (width/2), (Screen.height/2) - (height/2), width, height);
        }

        private void OnDestroy()
        {
            open = false;
        }

        private void PreventClickThrough()
        {
            bool cursorInGUI = false;
            EditorLogic EdLogInstance = EditorLogic.fetch;
            if (!EdLogInstance)
            {
                return;
            }
            if (open)
            {
                cursorInGUI = windowRect.Contains(GetMousePos());
            }
            if (cursorInGUI)
            {
                if (!CameraMouseLook.GetMouseLook())
                    EdLogInstance.Lock(false, false, false, "BDABELTLOCK");
                else
                    EdLogInstance.Unlock("BDABELTLOCK");
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("BDABELTLOCK");
            }
        }
        private Vector3 GetMousePos()
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            return mousePos;
        }
    }
}
