﻿using OWML.Common;
using QSB.HUD.Messages;
using QSB.Localization;
using QSB.Messaging;
using QSB.Player;
using QSB.ServerSettings;
using QSB.Utility;
using QSB.WorldSync;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QSB.HUD;

internal class MultiplayerHUDManager : MonoBehaviour, IAddComponentOnStart
{
	public static MultiplayerHUDManager Instance;

	private Transform _playerList;
	private Transform _textChat;
	private InputField _inputField;
	private Material _markerMaterial;

	public static Sprite UnknownSprite;
	public static Sprite DeadSprite;
	public static Sprite SpaceSprite;
	public static Sprite ShipSprite;
	public static Sprite TimberHearth;
	public static Sprite Attlerock;
	public static Sprite CaveTwin;
	public static Sprite TowerTwin;
	public static Sprite BrittleHollow;
	public static Sprite HollowsLantern;
	public static Sprite GiantsDeep;
	public static Sprite DarkBramble;
	public static Sprite Interloper;
	public static Sprite WhiteHole;

	public static readonly ListStack<HUDIcon> HUDIconStack = new(true);

	private void Start()
	{
		Instance = this;

		GlobalMessenger.AddListener(OWEvents.WakeUp, OnWakeUp);

		QSBPlayerManager.OnAddPlayer += OnAddPlayer;
		QSBPlayerManager.OnRemovePlayer += OnRemovePlayer;

		UnknownSprite = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_unknown.png");
		DeadSprite = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_dead.png");
		ShipSprite = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_ship.png");
		CaveTwin = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_cavetwin.png");
		TowerTwin = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_towertwin.png");
		TimberHearth = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_timberhearth.png");
		Attlerock = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_attlerock.png");
		BrittleHollow = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_brittlehollow.png");
		HollowsLantern = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_hollowslantern.png");
		GiantsDeep = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_giantsdeep.png");
		DarkBramble = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_darkbramble.png");
		Interloper = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_interloper.png");
		WhiteHole = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_whitehole.png");
		SpaceSprite = QSBCore.HUDAssetBundle.LoadAsset<Sprite>("Assets/MULTIPLAYER_UI/playerbox_space.png");
	}

	private const int LINE_COUNT = 11;
	private const int CHAR_COUNT = 41;
	private const float FADE_DELAY = 5f;
	private const float FADE_TIME = 2f;

	private bool _writingMessage;
	private readonly (string msg, Color color)[] _lines = new (string msg, Color color)[LINE_COUNT];
	// this should really be a deque, but eh
	private readonly ListStack<(string msg, Color color)> _messages = new(false);
	private float _lastMessageTime;

	public void WriteMessage(string message, Color color)
	{
		/* Tricky problem to solve.
		 * - 11 available lines for text to fit onto
		 * - Each line can be max 41 characters
		 * - Newest messages appear at the bottom, and get pushed up by newer messages.
		 * - Messages can use several lines.
		 * 
		 * From newest to oldest message, work out how many lines it needs
		 * and set the lines correctly bottom-up.
		 */

		_lastMessageTime = Time.time;

		_messages.Push((message, color));

		if (_messages.Count > LINE_COUNT)
		{
			_messages.RemoveFirstElementAndShift();
		}

		var currentLineIndex = 10;

		foreach (var msg in _messages.Reverse())
		{
			var characterCount = msg.msg.Length;
			var linesNeeded = Mathf.CeilToInt((float)characterCount / CHAR_COUNT);
			var chunk = 0;
			for (var i = linesNeeded - 1; i >= 0; i--)
			{
				if (currentLineIndex - i < 0)
				{
					chunk++;
					continue;
				}

				var chunkString = string.Concat(msg.msg.Skip(CHAR_COUNT * chunk).Take(CHAR_COUNT));
				_lines[currentLineIndex - i] = (chunkString, msg.color);
				chunk++;
			}

			currentLineIndex -= linesNeeded;

			if (currentLineIndex < 0)
			{
				break;
			}
		}

		var finalText = "";
		foreach (var line in _lines)
		{
			var msgColor = ColorUtility.ToHtmlStringRGBA(line.color);
			var msg = $"<color=#{msgColor}>{line.msg}</color>";

			if (line == default)
			{
				finalText += Environment.NewLine;
			}
			else if (line.msg.Length == CHAR_COUNT + 1)
			{
				finalText += msg;
			}
			else
			{
				finalText += $"{msg}{Environment.NewLine}";
			}
		}

		_textChat.Find("Messages").Find("Message").GetComponent<Text>().text = finalText;

		if (Locator.GetPlayerSuit().IsWearingHelmet())
		{
			var audioController = Locator.GetPlayerAudioController();
			audioController.PlayNotificationTextScrolling();
			Delay.RunFramesLater(10, () => audioController.StopNotificationTextScrolling());
		}

		_textChat.GetComponent<CanvasGroup>().alpha = 1;
	}

	private void Update()
	{
		if (!QSBWorldSync.AllObjectsReady || _playerList == null)
		{
			return;
		}

		_playerList.gameObject.SetActive(ServerSettingsManager.ShowExtraHUD);

		var inSuit = Locator.GetPlayerSuit().IsWearingHelmet();

		if (OWInput.IsNewlyPressed(InputLibrary.enter, InputMode.Character) && !_writingMessage && inSuit && QSBCore.TextChatInput)
		{
			OWInput.ChangeInputMode(InputMode.KeyboardInput);
			_writingMessage = true;
			_inputField.ActivateInputField();
			_textChat.GetComponent<CanvasGroup>().alpha = 1;
		}

		if (OWInput.IsNewlyPressed(InputLibrary.enter, InputMode.KeyboardInput) && _writingMessage)
		{
			OWInput.RestorePreviousInputs();
			_writingMessage = false;
			_inputField.DeactivateInputField();

			var message = _inputField.text;
			_inputField.text = "";
			message = message.Replace("\n", "").Replace("\r", "");
			message = $"{QSBPlayerManager.LocalPlayer.Name}: {message}";
			new ChatMessage(message, Color.white).Send();
		}

		if (OWInput.IsNewlyPressed(InputLibrary.escape, InputMode.KeyboardInput) && _writingMessage)
		{
			OWInput.RestorePreviousInputs();
			_writingMessage = false;
		}

		if (_writingMessage)
		{
			_lastMessageTime = Time.time;
		}

		if (!_writingMessage
			&& Time.time > _lastMessageTime + FADE_DELAY
			&& Time.time < _lastMessageTime + FADE_DELAY + FADE_TIME + 1)
		{
			var difference = Time.time - (_lastMessageTime + FADE_DELAY);
			var alpha = Mathf.Lerp(1, 0, difference / FADE_TIME);
			_textChat.GetComponent<CanvasGroup>().alpha = alpha;
		}
	}

	private void OnWakeUp()
	{
		var hudController = Locator.GetPlayerCamera().transform.Find("Helmet").Find("HUDController").GetComponent<HUDCanvas>();
		var hudCamera = hudController._hudCamera;
		var hudCanvas = hudCamera.transform.parent.Find("UICanvas");

		var multiplayerGroup = Instantiate(QSBCore.HUDAssetBundle.LoadAsset<GameObject>("assets/Prefabs/multiplayergroup.prefab"));

		Delay.RunNextFrame(() =>
		{
			// no idea why this has to be next frame, but it does
			multiplayerGroup.transform.parent = hudCanvas;
			multiplayerGroup.transform.localPosition = Vector3.zero;
			var rect = multiplayerGroup.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(1, 0.5f);
			rect.anchorMax = new Vector2(1, 0.5f);
			rect.sizeDelta = new Vector2(100, 100);
			rect.anchoredPosition3D = new Vector3(-267, 0, 0);
			rect.localRotation = Quaternion.Euler(0, 55, 0);
			rect.localScale = Vector3.one;
		});

		_playerList = multiplayerGroup.transform.Find("PlayerList");

		foreach (var player in QSBPlayerManager.PlayerList)
		{
			AddBox(player);

			foreach (var item in QSBWorldSync.GetUnityObjects<Minimap>())
			{
				AddMinimapMarker(player, item);
			}
		}

		CreateTrigger("TowerTwin_Body/Sector_TowerTwin", HUDIcon.TOWER_TWIN);
		CreateTrigger("CaveTwin_Body/Sector_CaveTwin", HUDIcon.CAVE_TWIN);
		CreateTrigger("TimberHearth_Body/Sector_TH", HUDIcon.TIMBER_HEARTH);
		CreateTrigger("Moon_Body/Sector_THM", HUDIcon.ATTLEROCK);
		CreateTrigger("BrittleHollow_Body/Sector_BH", HUDIcon.BRITTLE_HOLLOW);
		CreateTrigger("VolcanicMoon_Body/Sector_VM", HUDIcon.HOLLOWS_LANTERN);
		CreateTrigger("GiantsDeep_Body/Sector_GD", HUDIcon.GIANTS_DEEP);
		CreateTrigger("DarkBramble_Body/Sector_DB", HUDIcon.DARK_BRAMBLE);
		CreateTrigger("Comet_Body/Sector_CO", HUDIcon.INTERLOPER);
		CreateTrigger("WhiteHole_Body/Sector_WhiteHole", HUDIcon.WHITE_HOLE);

		HUDIconStack.Clear();
		HUDIconStack.Push(HUDIcon.SPACE);
		HUDIconStack.Push(HUDIcon.TIMBER_HEARTH);

		new PlanetMessage(HUDIcon.TIMBER_HEARTH).Send();

		_textChat = multiplayerGroup.transform.Find("TextChat");
		var inputFieldGO = _textChat.Find("InputField");
		_inputField = inputFieldGO.GetComponent<InputField>();
		_inputField.text = "";
		_textChat.Find("Messages").Find("Message").GetComponent<Text>().text = "";
		_lines.Clear();
		_messages.Clear();
		_textChat.GetComponent<CanvasGroup>().alpha = 0;
	}

	public void UpdateMinimapMarkers(Minimap minimap)
	{
		var localRuleset = Locator.GetPlayerRulesetDetector().GetPlanetoidRuleset();

		foreach (var player in QSBPlayerManager.PlayerList)
		{
			if (player.IsDead || player.IsLocalPlayer || !player.IsReady)
			{
				continue;
			}

			if (player.RulesetDetector == null)
			{
				if (player.Body != null)
				{
					DebugLog.ToConsole($"Error - {player.PlayerId}'s RulesetDetector is null.", MessageType.Error);
				}

				continue;
			}

			if (player.RulesetDetector.GetPlanetoidRuleset() == null
				|| player.RulesetDetector.GetPlanetoidRuleset() != localRuleset)
			{
				continue;
			}

			if (player.MinimapPlayerMarker == null)
			{
				continue;
			}

			if (ServerSettingsManager.ShowExtraHUD)
			{
				player.MinimapPlayerMarker.localPosition = GetLocalMapPosition(player, minimap);
				player.MinimapPlayerMarker.LookAt(minimap._globeMeshTransform, minimap._globeMeshTransform.up);
				player.MinimapPlayerMarker.GetComponent<MeshRenderer>().enabled = true;
			}
			else
			{
				player.MinimapPlayerMarker.localPosition = Vector3.zero;
				player.MinimapPlayerMarker.localRotation = Quaternion.identity;
				player.MinimapPlayerMarker.GetComponent<MeshRenderer>().enabled = false;
			}
		}
	}

	public void HideMinimap(Minimap minimap)
	{
		foreach (var player in QSBPlayerManager.PlayerList)
		{
			if (player.MinimapPlayerMarker == null)
			{
				continue;
			}

			player.MinimapPlayerMarker.GetComponent<MeshRenderer>().enabled = false;
		}
	}

	public void ShowMinimap(Minimap minimap)
	{
		foreach (var player in QSBPlayerManager.PlayerList)
		{
			if (player.MinimapPlayerMarker == null)
			{
				continue;
			}

			player.MinimapPlayerMarker.GetComponent<MeshRenderer>().enabled = true;
		}
	}

	private void AddMinimapMarker(PlayerInfo player, Minimap minimap)
	{
		player.MinimapPlayerMarker = Instantiate(minimap._probeMarkerTransform);
		player.MinimapPlayerMarker.parent = minimap._probeMarkerTransform.parent;
		player.MinimapPlayerMarker.localScale = new Vector3(0.05f, 0.05f, 0.05f);
		player.MinimapPlayerMarker.localPosition = Vector3.zero;
		player.MinimapPlayerMarker.localRotation = Quaternion.identity;

		if (_markerMaterial == null)
		{
			var playerMinimap = QSBWorldSync.GetUnityObjects<Minimap>().First(x => x.name == "Minimap_Root");
			_markerMaterial = Instantiate(playerMinimap._probeMarkerTransform.GetComponent<MeshRenderer>().material);
			_markerMaterial.color = new Color32(218, 115, 255, 255);
		}

		player.MinimapPlayerMarker.GetComponent<MeshRenderer>().material = _markerMaterial;
	}

	private void AddBox(PlayerInfo player)
	{
		var box = Instantiate(QSBCore.HUDAssetBundle.LoadAsset<GameObject>("assets/Prefabs/playerbox.prefab"));
		box.transform.parent = _playerList;
		box.transform.localScale = new Vector3(1, 1, 1);
		box.transform.localPosition = Vector3.zero;
		box.transform.localRotation = Quaternion.identity;

		var boxScript = box.GetComponent<PlayerBox>();
		boxScript.AssignPlayer(player);
	}

	private Vector3 GetLocalMapPosition(PlayerInfo player, Minimap minimap)
	{
		return Vector3.Scale(
			player.RulesetDetector.GetPlanetoidRuleset().transform.InverseTransformPoint(player.Body.transform.position).normalized * 0.51f,
			minimap._globeMeshTransform.localScale);
	}

	private void OnAddPlayer(PlayerInfo player)
	{
		if (!QSBWorldSync.AllObjectsReady)
		{
			return;
		}

		AddBox(player);

		foreach (var item in QSBWorldSync.GetUnityObjects<Minimap>())
		{
			AddMinimapMarker(player, item);
		}
	}

	private void OnRemovePlayer(PlayerInfo player)
	{
		Destroy(player.HUDBox?.gameObject);
		Destroy(player.MinimapPlayerMarker);

		WriteMessage(string.Format(QSBLocalization.Current.PlayerLeftTheGame, player.Name), Color.yellow);
	}

	private PlanetTrigger CreateTrigger(string parentPath, HUDIcon icon)
		=> CreateTrigger(Find(parentPath), icon);

	private PlanetTrigger CreateTrigger(GameObject parent, HUDIcon icon)
	{
		if (parent == null)
		{
			return null;
		}

		var triggerGO = parent.FindChild("HUD_PLANET_TRIGGER");
		if (triggerGO != null)
		{
			var trigger = triggerGO.GetAddComponent<PlanetTrigger>();
			trigger.Icon = icon;
			return trigger;
		}
		else
		{
			triggerGO = new GameObject("HUD_PLANET_TRIGGER");
			triggerGO.transform.SetParent(parent.transform, false);
			triggerGO.SetActive(false);
			var trigger = triggerGO.AddComponent<PlanetTrigger>();
			trigger.Icon = icon;
			triggerGO.SetActive(true);
			return trigger;
		}
	}

	public static GameObject Find(string path)
	{
		var go = GameObject.Find(path);

		if (go == null)
		{
			// find inactive use root + transform.find
			var names = path.Split('/');
			var rootName = names[0];
			var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == rootName);
			if (root == null)
			{
				return null;
			}

			var childPath = string.Join("/", names.Skip(1));
			go = root.FindChild(childPath);
		}

		return go;
	}
}
