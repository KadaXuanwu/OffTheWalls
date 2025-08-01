using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeathOverlayContext : MonoBehaviour {
    [Header("Death UI")]
    public GameObject deathOverlay;
    public TextMeshProUGUI deathMessageText;
    public TextMeshProUGUI respawnTimerText;

    [Header("Upgrade UI")]
    public GameObject upgradePanel;
    public UpgradeButtonContext[] upgradeButtons = new UpgradeButtonContext[3];
}

[System.Serializable]
public class UpgradeButtonContext {
    public Button button;
    public Image upgradeIcon;
    public TextMeshProUGUI upgradeNameText;
    public TextMeshProUGUI upgradeDescriptionText;
}