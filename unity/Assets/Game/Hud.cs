using System;
using System.Collections.Generic;
using Rats.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rats.Game
{
    /// <summary>
    /// Player-facing HUD (uGUI, on its own camera so the retro effect doesn't blur text and headless captures
    /// can render it): round/score/timer, both HP bars and points, draft cards, result banners, damage pop-ups
    /// and the turn-end cue. Reads sim state only.
    /// </summary>
    sealed class Hud
    {
        const int CueTicks = 60;
        static readonly Color Ice = new Color(0.62f, 0.85f, 1f);
        static readonly Color Panel = new Color(0.08f, 0.06f, 0.05f, 0.72f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Blood = new Color(0.78f, 0.16f, 0.12f);
        const float PopupSeconds = 1f;

        readonly Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        readonly RectTransform root;
        readonly Camera worldCamera, uiCamera;
        readonly Text roundText, timerText, bannerText, bannerSub, frozenText, promptText, ringText;
        readonly Image timerFill, cueTint;
        readonly Text myName, myHpText, myUpgrades, theirName, theirHpText, theirUpgrades;
        readonly Image myHp, theirHp;
        readonly Image[] myPips = new Image[6];
        readonly GameObject draftPanel;
        readonly Text draftTitle, draftStatus, draftHint;
        readonly Button[] cards = new Button[3];
        readonly Text[] cardTexts = new Text[3];
        readonly List<(Text text, Vector3 world, float born)> popups = new List<(Text, Vector3, float)>();
        float frozeAt = -10f;
        float ringAt = -10f;
        double lastFireScale = 1;

        public Action<int> OnPickDraft;

        /// <summary>Real time, which also advances in headless edit-mode captures (unscaledTime doesn't).</summary>
        static float Now => Time.realtimeSinceStartup;

        public Hud(Transform parent, Camera worldCamera, Camera uiCamera)
        {
            this.worldCamera = worldCamera;
            this.uiCamera = uiCamera;
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            cueTint = Img(root, "CueTint", new Color(Ice.r, Ice.g, Ice.b, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Top center: round and score, timer bar.
            roundText = Txt(root, "Round", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(700, 36), Color.white, true);
            timerText = Txt(root, "Timer", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(400, 24), new Color(0.9f, 0.85f, 0.75f), true);
            var timerBack = Img(root, "TimerBack", Panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(300, 8));
            timerFill = Img(timerBack.rectTransform, "TimerFill", Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Bottom left: you.
            var me = Img(root, "You", Panel, Vector2.zero, Vector2.zero, new Vector2(196, 64), new Vector2(360, 104));
            myName = Txt(me.rectTransform, "Name", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(150, -16), new Vector2(280, 24), Color.white, true);
            myHp = Bar(me.rectTransform, new Vector2(0, -42), Blood, out myHpText);
            for (int i = 0; i < myPips.Length; i++)
                myPips[i] = Img(me.rectTransform, "Pip", Gold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22 + i * 26, -72), new Vector2(18, 18));
            myUpgrades = Txt(me.rectTransform, "Upgrades", 13, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(180, -94), new Vector2(340, 18), new Color(0.85f, 0.8f, 0.7f), false);

            // Top right: opponent.
            var them = Img(root, "Them", Panel, Vector2.one, Vector2.one, new Vector2(-196, -60), new Vector2(360, 92));
            theirName = Txt(them.rectTransform, "Name", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(150, -16), new Vector2(280, 24), Color.white, true);
            theirHp = Bar(them.rectTransform, new Vector2(0, -42), Blood, out theirHpText);
            theirUpgrades = Txt(them.rectTransform, "Upgrades", 13, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(180, -72), new Vector2(340, 18), new Color(0.85f, 0.8f, 0.7f), false);

            // Center: result banner and FROZEN flash.
            bannerText = Txt(root, "Banner", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1100, 60), Gold, true);
            bannerSub = Txt(root, "BannerSub", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 74), new Vector2(900, 30), Color.white, true);
            frozenText = Txt(root, "Frozen", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(800, 70), Ice, true);
            promptText = Txt(root, "Prompt", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1100, 44), new Color(1f, 0.45f, 0.3f), true);
            ringText = Txt(root, "Ring", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(900, 54), new Color(1f, 0.55f, 0.2f), true);

            // Bottom center: draft cards.
            var draft = Img(root, "Draft", Panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(640, 270));
            draftPanel = draft.gameObject;
            draftTitle = Txt(draft.rectTransform, "Title", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(620, 26), Gold, true);
            draftStatus = Txt(draft.rectTransform, "Status", 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(620, 20), new Color(0.85f, 0.8f, 0.7f), false);
            for (int i = 0; i < 3; i++)
            {
                int slot = i + 1;
                var card = Img(draft.rectTransform, "Card", new Color(0.22f, 0.17f, 0.13f, 0.95f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -86 - i * 58), new Vector2(600, 52));
                cards[i] = card.gameObject.AddComponent<Button>();
                cards[i].onClick.AddListener(() => OnPickDraft?.Invoke(slot));
                cardTexts[i] = Txt(card.rectTransform, "Text", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(590, 50), Color.white, false);
            }
            draftHint = Txt(draft.rectTransform, "Hint", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(620, 20), new Color(0.75f, 0.7f, 0.62f), false);
        }

        public void Popup(Vector3 world, string text, Color color, int size)
        {
            var t = Txt(root, "Popup", size, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 44), color, true);
            popups.Add((t, world, Now));
        }

        public void FreezeStarted() => frozeAt = Now;

        public void Sync(Simulation sim, int local, string theirLabel, bool isClient)
        {
            var me = sim.Fighters[local];
            var them = sim.Fighters[1 - local];
            roundText.text = $"ROUND {sim.Round}     YOU {sim.Score(local)} - {sim.Score(1 - local)} {theirLabel.ToUpperInvariant()}";

            float turnFraction = 0f;
            switch (sim.Phase)
            {
                case Phase.Combat:
                    timerText.text = $"TURN {sim.Turn}   {sim.PhaseTicksLeft / 60f:F1}s";
                    turnFraction = sim.PhaseTicksLeft / (float)sim.T.TurnTicks;
                    timerFill.color = sim.PhaseTicksLeft <= CueTicks ? Ice : Gold;
                    break;
                case Phase.Freeze:
                    timerText.text = sim.DraftActive ? $"DRAFT   {sim.PhaseTicksLeft / 60f:F1}s" : $"FROZEN   {sim.PhaseTicksLeft / 60f:F1}s";
                    turnFraction = 0f;
                    break;
                default:
                    timerText.text = "";
                    break;
            }
            timerFill.rectTransform.anchorMax = new Vector2(turnFraction, 1f);

            float cue = sim.Phase == Phase.Combat && sim.PhaseTicksLeft <= CueTicks ? 1f - sim.PhaseTicksLeft / (float)CueTicks : 0f;
            cueTint.color = new Color(Ice.r, Ice.g, Ice.b, 0.2f * cue);
            float sinceFreeze = Now - frozeAt;
            frozenText.text = sinceFreeze < 0.7f ? "FROZEN" : "";
            frozenText.color = new Color(Ice.r, Ice.g, Ice.b, Mathf.Clamp01(1f - sinceFreeze / 0.7f));

            myName.text = $"YOU   {(me.LockedOn ? "LOCKED" : "free")}";
            SetBar(myHp, myHpText, me);
            for (int i = 0; i < myPips.Length; i++)
            {
                myPips[i].gameObject.SetActive(i < sim.T.ActionPointsPerTurn);
                myPips[i].color = i < me.ActionPoints ? Gold : new Color(0.25f, 0.22f, 0.2f);
            }
            myUpgrades.text = Owned(me);
            theirName.text = $"{theirLabel.ToUpperInvariant()}   {them.ActionPoints} AP";
            SetBar(theirHp, theirHpText, them);
            theirUpgrades.text = Owned(them);

            // Downed / execution prompts and the ring announcement.
            string prompt = "";
            if (sim.Phase == Phase.Combat || sim.Phase == Phase.Freeze)
            {
                if (me.Downed) prompt = $"YOU ARE DOWN   {me.DownedTicksLeft / 60f:F1}s";
                else if (them.Downed)
                    prompt = me.Action == ActionKind.Execute ? "EXECUTING..."
                           : Vec3.DistanceXZ(me.Position, them.Position) <= sim.T.ExecuteRange ? "EXECUTE!   PRESS J"
                           : $"{theirLabel.ToUpperInvariant()} IS DOWN - GET CLOSE AND PRESS J   {them.DownedTicksLeft / 60f:F1}s";
            }
            promptText.text = prompt;
            if (lastFireScale >= 1 && sim.FireScale < 1) ringAt = Now;
            lastFireScale = sim.FireScale;
            float sinceRing = Now - ringAt;
            ringText.text = sinceRing < 2.5f ? "THE RING CLOSES" : "";
            ringText.color = new Color(1f, 0.55f, 0.2f, Mathf.Clamp01(2.5f - sinceRing));

            bannerText.text = bannerSub.text = "";
            string theirs = theirLabel.ToUpperInvariant();
            if (sim.Phase == Phase.RoundOver)
            {
                bannerText.text = sim.Winner == Simulation.Draw ? "DRAW - ROUND REPLAYED" : sim.Winner == local ? "ROUND TO YOU" : $"ROUND TO {theirs}";
                bannerSub.text = $"Next round in {sim.PhaseTicksLeft / 60f:F1}s";
            }
            else if (sim.Phase == Phase.MatchOver)
            {
                bannerText.text = (sim.MatchWinner == local ? "YOU WIN THE MATCH" : $"{theirs} WINS THE MATCH") + $"  {sim.Score(local)}-{sim.Score(1 - local)}";
                bannerSub.text = isClient ? "Waiting for the host to start a new match" : "Enter for a new match";
            }

            bool drafting = sim.DraftActive && sim.Phase == Phase.Freeze;
            draftPanel.SetActive(drafting);
            if (drafting)
            {
                draftTitle.text = $"DRAFT {sim.DraftsThisRound}/{sim.T.DraftsPerRound}   PICK ONE (1-3 OR CLICK)   {sim.PhaseTicksLeft / 60f:F1}s";
                draftStatus.text = them.DraftPick != 0 ? $"{theirLabel} has picked." : $"{theirLabel} is choosing...";
                for (int i = 0; i < 3; i++)
                {
                    int offer = Simulation.Offer(me, i);
                    cards[i].gameObject.SetActive(offer != Simulation.NoOffer);
                    if (offer == Simulation.NoOffer) continue;
                    var u = (UpgradeId)offer;
                    bool picked = me.DraftPick == offer + 1;
                    cardTexts[i].text = $"<b>[{i + 1}] {Upgrades.Name(u)}{(picked ? "   <- YOUR PICK" : "")}</b>\n{Upgrades.Describe(u, sim.T)}";
                    cardTexts[i].color = picked ? Gold : Color.white;
                    cards[i].interactable = me.DraftPick == 0;
                }
                draftHint.text = me.DraftPick == 0 ? "No pick when the timer ends = your first offer." : "Locked in. Applies when time resumes.";
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (text, world, born) = popups[i];
                float age = Now - born;
                if (age > PopupSeconds) { UnityEngine.Object.Destroy(text.gameObject); popups.RemoveAt(i); continue; }
                var screen = worldCamera.WorldToScreenPoint(world + Vector3.up * age * 0.8f);
                text.enabled = screen.z > 0;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var localPoint))
                    text.rectTransform.anchoredPosition = localPoint;
                var c = text.color;
                text.color = new Color(c.r, c.g, c.b, 1f - age / PopupSeconds);
            }
        }

        static string Owned(Fighter f)
        {
            var names = new List<string>();
            foreach (var u in Upgrades.Pool) if (f.Has(u)) names.Add(Upgrades.Name(u));
            return names.Count == 0 ? "" : string.Join("  ·  ", names);
        }

        static void SetBar(Image fill, Text label, Fighter f)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f.Hp / (float)f.MaxHp), 1f);
            label.text = f.Downed ? $"DOWN  {f.DownedTicksLeft / 60f:F1}s" : $"{Mathf.Max(0, f.Hp)} / {f.MaxHp}";
        }

        Image Bar(RectTransform parent, Vector2 pos, Color color, out Text label)
        {
            var back = Img(parent, "HpBack", new Color(0.18f, 0.14f, 0.12f), new Vector2(0, 1), new Vector2(0, 1), pos + new Vector2(170, 0), new Vector2(320, 18));
            var fill = Img(back.rectTransform, "HpFill", color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label = Txt(back.rectTransform, "HpText", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 18), Color.white, true);
            return fill;
        }

        static Image Img(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (anchorMin != anchorMax) rt.offsetMin = rt.offsetMax = Vector2.zero; // stretched (fills, tints): anchors set the size
            else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = name == "Card";
            return img;
        }

        Text Txt(RectTransform parent, string name, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color color, bool shadow)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (shadow) go.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
