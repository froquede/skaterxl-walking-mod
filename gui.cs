using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    class gui : MonoBehaviour
    {
        public bool visible = true, emote_config = false, sound_emote_config = false;
        Texture2D circularTexture, bg_center, select_right, select_left, select_up, select_down, dpad_icon, user_icon, sound_icon;
        Texture2D list_bg, selected_bg, change_emote_btn, confirm_btn, cancel_btn, dpad_input;
        private Rect windowRect = new Rect((Screen.width / 60), (Screen.width / 60), 200, 200);
        string dpad = "";
        bool lb, rb;
        Font futura;
        GUIStyle style, debugStyle;
        Font font;

        public void Start()
        {
            circularTexture = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\bg.png"));
            bg_center = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\bg_center.png"));
            select_right = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\select_right.png"));
            select_left = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\select_left.png"));
            select_up = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\select_up.png"));
            select_down = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\select_down.png"));
            dpad_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\dpad.png"));
            user_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\user_icon.png"));
            sound_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\sound_icon.png"));
            list_bg = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\emote_select_bg.png"));
            selected_bg = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\selected_bg.png"));
            change_emote_btn = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\change_emote_btn.png"));
            confirm_btn = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\confirm_btn.png"));
            cancel_btn = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\cancel_btn.png"));
            dpad_input = LoadTexture(Path.Combine(Main.modEntry.Path, "ui\\dpad_input.png"));

            futura = (Font)Resources.Load("FuturaStd-BookOblique");
            font = Font.CreateDynamicFontFromOSFont("Bahnschrift", 24);

            style = new GUIStyle();
            style.font = font;
            style.normal.textColor = new Color32(77, 77, 77, 255);
            style.fontSize = 16;
            style.richText = true;
            //style.alignment = TextAnchor.MiddleCenter;

            debugStyle = new GUIStyle();
            debugStyle.normal = new GUIStyleState();
            debugStyle.normal.textColor = Color.black;
            debugStyle.normal.background = Texture2D.redTexture;


            //Screen.SetResolution(1080 / 2, 1920 / 2, false);
        }

        Texture2D LoadTexture(string filename)
        {
            byte[] rawData = File.ReadAllBytes(filename);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(rawData);
            return texture;
        }

        int config_count = 0;
        bool show_change_btn = false;
        public void Update()
        {
            lb = rb = false;
            if (PlayerController.Instance.inputController.player.GetButtonDown("LB") || PlayerController.Instance.inputController.player.GetButton("LB"))
            {
                visible = true;
                lb = true;
            }
            else
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown("RB") || PlayerController.Instance.inputController.player.GetButton("RB"))
                {
                    visible = true;
                    rb = true;
                }
                else if(!emote_config) resetState();
            }

            dpad = "";
            if (GetButtonDown(70)) dpad = "left";
            if (GetButtonDown(68)) dpad = "down";
            if (GetButtonDown(69)) dpad = "right";
            if (GetButtonDown(67)) dpad = "top";

            if (emote_config)
            {
                if (config_count > 48) ChangeSelect(dpad);
                dpad = "";

                if (GetButtonDown("B"))
                {
                    emote_config = false;
                    UISounds.Instance.PlayOneShotExit();
                }

                if (GetButtonDown("A") && config_count > 48)
                {
                    Main.walking_go.changeEmote(Main.walking_go.emotes[selected]);
                    UISounds.Instance.PlayOneShotSelectMajor();
                    emote_config = false;
                }

                config_count++;
            }
            else config_count = 0;

            if (dpad != "") show_change_btn = true;
            else show_change_btn = false;

            if (!Main.walking_go.inState && !emote_config) resetState();
        }

        bool GetButtonDown(string button)
        {
            return PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        bool GetButtonDown(int button)
        {
            return PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        int debounce = 0;
        void ChangeSelect(string dpad)
        {
            if (debounce > 12)
            {
                if (dpad == "top" && selected > 0) {
                    selected--;
                    UISounds.Instance.PlayOneShotSelectionChange();
                } 
                if (dpad == "down" && selected < Main.walking_go.emotes.Count - 1)
                {
                    selected++;
                    UISounds.Instance.PlayOneShotSelectionChange();
                }
                debounce = 0;
            }
            debounce++;
        }

        void resetState()
        {
            visible = false;
            emote_config = false;
            debounce = 0;
            config_count = 0;
        }

        public int selected = 0;
        private void OnGUI()
        {

            if (!visible) return;

            GUI.DrawTexture(new Rect(Screen.width / 60, (Screen.height / 2) - circularTexture.height / 2, circularTexture.width, circularTexture.height), circularTexture);
            if (dpad == "left" || (emote_config && Main.walking_go.last_dpad == 70)) GUI.DrawTexture(new Rect((Screen.width / 60) + 3, (Screen.height / 2) - select_left.height / 2, select_left.width, select_left.height), select_left);
            if (dpad == "down" || (emote_config && Main.walking_go.last_dpad == 68)) GUI.DrawTexture(new Rect((Screen.width / 60) + 33f, (Screen.height / 2) + 23, select_down.width, select_down.height), select_down);
            if (dpad == "right" || (emote_config && Main.walking_go.last_dpad == 69)) GUI.DrawTexture(new Rect((Screen.width / 60) + 126.5f, (Screen.height / 2) - select_right.height / 2, select_right.width, select_right.height), select_right);
            if (dpad == "top" || (emote_config && Main.walking_go.last_dpad == 67)) GUI.DrawTexture(new Rect((Screen.width / 60) + 33f, (Screen.height / 2) - (select_up.height + 23), select_up.width, select_up.height), select_up);
            GUI.DrawTexture(new Rect((Screen.width / 60) + 68f, (Screen.height / 2) - bg_center.height / 2, bg_center.width, bg_center.height), bg_center);

            GUI.DrawTexture(new Rect((Screen.width / 60) + 54.5f, (Screen.height / 2) - dpad_icon.height / 2, dpad_icon.width, dpad_icon.height), dpad_icon);
            if (lb || emote_config) GUI.DrawTexture(new Rect((Screen.width / 60) + 88f, (Screen.height / 2) - user_icon.height / 2, user_icon.width, user_icon.height), user_icon);
            if (rb) GUI.DrawTexture(new Rect((Screen.width / 60) + 86f, (Screen.height / 2) - sound_icon.height / 2, sound_icon.width, sound_icon.height), sound_icon);

            if (lb || emote_config)
            {
                try
                {
                    EllipsisText(new Rect((Screen.width / 60) + 156f, (Screen.height / 2) - 42.5f, 32, 85), Main.walking_go.emote3.name.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 16f, (Screen.height / 2) - 42.5f, 32, 85), Main.walking_go.emote1.name.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) - 87, 85, 32), Main.walking_go.emote4.name.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) + 53, 85, 32), Main.walking_go.emote2.name.ToUpper());
                }
                catch
                {
                    UnityModManager.Logger.Log("Error ui " + (Main.walking_go.emote3 == null));
                }

                if (show_change_btn) GUI.DrawTexture(new Rect(36 + (Screen.width / 60), (Screen.height / 2) + (circularTexture.height / 2) + 12, change_emote_btn.width, change_emote_btn.height), change_emote_btn);
            }

            if (rb)
            {
                try
                {
                    EllipsisText(new Rect((Screen.width / 60) + 156f, (Screen.height / 2) - 42.5f, 32, 85), Main.settings.semote3.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 16f, (Screen.height / 2) - 42.5f, 32, 85), Main.settings.semote1.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) - 87, 85, 32), Main.settings.semote4.ToUpper());
                    EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) + 53, 85, 32), Main.settings.semote2.ToUpper());
                }
                catch
                {
                    UnityModManager.Logger.Log("Error ui " + (Main.walking_go.emote3 == null));
                }

                //if (show_change_btn) GUI.DrawTexture(new Rect(36 + (Screen.width / 60), (Screen.height / 2) + (circularTexture.height / 2) + 12, change_emote_btn.width, change_emote_btn.height), change_emote_btn);
            }

            if (emote_config)
            {
                int origin_list_x = (Screen.width / 60) + 220;
                int origin_list_y = (Screen.height / 2) - list_bg.height / 2;
                GUI.DrawTexture(new Rect(origin_list_x, origin_list_y, list_bg.width, list_bg.height), list_bg);

                int length = selected + 6;
                if (length > Main.walking_go.emotes.Count) length = Main.walking_go.emotes.Count;

                GUI.DrawTexture(new Rect(origin_list_x, origin_list_y + 8, selected_bg.width, selected_bg.height), selected_bg);

                for (int i = selected; i < length; i++)
                {
                    int offset = 16 + (32 * (i - selected));
                    EllipsisText(new Rect(origin_list_x + 20, origin_list_y + offset, 154, 34), "" + Main.walking_go.emotes[i].ToUpper() + "", false);
                }

                GUI.DrawTexture(new Rect(origin_list_x + list_bg.width, origin_list_y - 6 + (dpad_input.height / 2), dpad_input.width, dpad_input.height), dpad_input);

                int offset_btn = 16 + (32 * 6);
                GUI.DrawTexture(new Rect(origin_list_x + 36, origin_list_y + offset_btn + 4, confirm_btn.width, confirm_btn.height), confirm_btn);
                GUI.DrawTexture(new Rect(origin_list_x + 38, origin_list_y + offset_btn + confirm_btn.height + 2, cancel_btn.width, cancel_btn.height), cancel_btn);
            }
        }

        void EllipsisText(Rect rect, string text, bool center = true)
        {
            try
            {
                /*Color originalColor = GUI.color;
                GUI.color = Color.red;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = originalColor;*/

                string displayText = text;
                float textWidth = style.CalcSize(new GUIContent(displayText)).x;
                float rectWidth = rect.width;

                if (textWidth > rectWidth)
                {
                    string ellipsis = "...";
                    float rectHeight = rect.height;
                    float lineHeight = style.lineHeight;
                    int maxLines = Mathf.FloorToInt(rectHeight / lineHeight);

                    string[] characters = text.ToCharArray().Select(c => c.ToString()).ToArray();
                    displayText = "";
                    int lineCount = 1;
                    for (int i = 0; i < characters.Length; i++)
                    {
                        string character = characters[i];
                        string testString = displayText + character;
                        textWidth = style.CalcSize(new GUIContent(testString)).x;
                        if (textWidth > rectWidth || lineCount == maxLines)
                        {
                            if (displayText == "")
                            {
                                displayText = character;
                            }
                            else
                            {
                                displayText += "\n" + character;
                            }
                            lineCount++;
                        }
                        else
                        {
                            displayText += character;
                        }
                    }

                    while (style.CalcSize(new GUIContent(displayText)).y > rectHeight && displayText.Contains("\n"))
                    {
                        int lastLineBreak = displayText.LastIndexOf("\n");
                        displayText = displayText.Substring(0, lastLineBreak);
                    }

                    if (style.CalcSize(new GUIContent(displayText)).y > rectHeight)
                    {
                        while (textWidth > rectWidth - style.fontSize)
                        {
                            displayText = displayText.Substring(0, displayText.Length - 1);
                            textWidth = style.CalcSize(new GUIContent(displayText + ellipsis)).x;
                        }
                        displayText += ellipsis;
                    }
                }

                Vector2 textSize = style.CalcSize(new GUIContent(displayText));
                float x = rect.x + (rect.width - textSize.x) / 2;
                float y = rect.y + (rect.height - textSize.y) / 2;

                if (center)
                {
                    Rect centeredRect = new Rect(x, y, rect.width, rect.height);
                    GUI.Label(centeredRect, displayText, style);
                }
                else GUI.Label(rect, displayText, style);
            }
            catch
            {
                GUI.Label(rect, text);
            }
        }
    }
}
