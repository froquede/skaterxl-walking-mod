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
        public bool visible = true;
        Texture2D circularTexture, bg_center, select_right, select_left, select_up, select_down, dpad_icon, user_icon, sound_icon;
        private Rect windowRect = new Rect((Screen.width / 60), (Screen.width / 60), 200, 200);
        string dpad = "";
        bool lb, rb;
        Font futura;
        GUIStyle style, debugStyle;

        public void Start()
        {
            circularTexture = LoadTexture(Path.Combine(Main.modEntry.Path, "bg.png"));
            bg_center = LoadTexture(Path.Combine(Main.modEntry.Path, "bg_center.png"));
            select_right = LoadTexture(Path.Combine(Main.modEntry.Path, "select_right.png"));
            select_left = LoadTexture(Path.Combine(Main.modEntry.Path, "select_left.png"));
            select_up = LoadTexture(Path.Combine(Main.modEntry.Path, "select_up.png"));
            select_down = LoadTexture(Path.Combine(Main.modEntry.Path, "select_down.png"));
            dpad_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "dpad.png"));
            user_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "user_icon.png"));
            sound_icon = LoadTexture(Path.Combine(Main.modEntry.Path, "sound_icon.png"));

            futura = (Font)Resources.Load("FuturaStd-BookOblique");

            style = new GUIStyle();
            style.font = futura;
            style.normal.textColor = new Color32(77, 77, 77, 255);
            style.fontSize = 16;
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
                else visible = false;
            }

            dpad = "";
            if (PlayerController.Instance.inputController.player.GetButtonDown(70) || PlayerController.Instance.inputController.player.GetButton(70)) dpad = "left";
            if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) dpad = "down";
            if (PlayerController.Instance.inputController.player.GetButtonDown(69) || PlayerController.Instance.inputController.player.GetButton(69)) dpad = "right";
            if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) dpad = "top";

            if (!Main.walking_go.inState) visible = false;
        }

        private void OnGUI()
        {
            if (!visible) return;
            GUI.DrawTexture(new Rect(Screen.width / 60, (Screen.height / 2) - circularTexture.height / 2, circularTexture.width, circularTexture.height), circularTexture);
            if (dpad == "right") GUI.DrawTexture(new Rect((Screen.width / 60) + 126.5f, (Screen.height / 2) - select_right.height / 2, select_right.width, select_right.height), select_right);
            if (dpad == "left") GUI.DrawTexture(new Rect((Screen.width / 60) + 3, (Screen.height / 2) - select_left.height / 2, select_left.width, select_left.height), select_left);
            if (dpad == "top") GUI.DrawTexture(new Rect((Screen.width / 60) + 33f, (Screen.height / 2) - (select_up.height + 23), select_up.width, select_up.height), select_up);
            if (dpad == "down") GUI.DrawTexture(new Rect((Screen.width / 60) + 33f, (Screen.height / 2) + 23, select_down.width, select_down.height), select_down);
            GUI.DrawTexture(new Rect((Screen.width / 60) + 68f, (Screen.height / 2) - bg_center.height / 2, bg_center.width, bg_center.height), bg_center);

            GUI.DrawTexture(new Rect((Screen.width / 60) + 54.5f, (Screen.height / 2) - dpad_icon.height / 2, dpad_icon.width, dpad_icon.height), dpad_icon);
            if (lb) GUI.DrawTexture(new Rect((Screen.width / 60) + 88f, (Screen.height / 2) - user_icon.height / 2, user_icon.width, user_icon.height), user_icon);
            if (rb) GUI.DrawTexture(new Rect((Screen.width / 60) + 86f, (Screen.height / 2) - sound_icon.height / 2, sound_icon.width, sound_icon.height), sound_icon);

            if(lb)
            {
                EllipsisText(new Rect((Screen.width / 60) + 156f, (Screen.height / 2) - 42.5f, 32, 85), Main.walking_go.emote3.name.ToUpper());
                EllipsisText(new Rect((Screen.width / 60) + 16f, (Screen.height / 2) - 42.5f, 32, 85), Main.walking_go.emote1.name.ToUpper());
                EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) - 87, 85, 32), Main.walking_go.emote4.name.ToUpper());
                EllipsisText(new Rect((Screen.width / 60) + 61, (Screen.height / 2) + 53, 85, 32), Main.walking_go.emote2.name.ToUpper());
            }
        }

        void EllipsisText(Rect rect, string text)
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
            Rect centeredRect = new Rect(x, y, rect.width, rect.height);
            GUI.Label(centeredRect, displayText, style);
            //GUI.Label(rect, displayText, style);
        }
    }
}
