namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.InputSystem;

  public class QuantumDebugInput : MonoBehaviour {
    [SerializeField] private PlayerInput playerInput;
    
    // Add fields for upgrade selection
    private int _pendingUpgradeSelection = -1;
    private static QuantumDebugInput _instance;

    private void Awake() {
      _instance = this;
    }

    private void OnEnable() {
      QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    public void PollInput(CallbackPollInput callback) {
      Input i = new();

      Vector2 movement = playerInput.actions["Move"].ReadValue<Vector2>();
      i.Direction = movement.ToFPVector2();
      i.Dash = playerInput.actions["Dash"].inProgress;
      i.Attack = playerInput.actions["Attack"].inProgress;
      i.SwitchWeapon = playerInput.actions["SwitchWeapon"].inProgress;
      i.ShowTrajectory = Mouse.current.rightButton.isPressed;
      Debug.Log(movement);

      Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
      Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0));
      i.MousePosition = new FPVector2(mouseWorldPos.x.ToFP(), mouseWorldPos.y.ToFP());

      // Add upgrade selection
      if (_pendingUpgradeSelection >= 0) {
        i.SelectedUpgradeIndex = _pendingUpgradeSelection + 1; // +1 to distinguish from default 0
        _pendingUpgradeSelection = -1; // Clear after sending
      }

      callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }

    // Public method to set upgrade selection
    public static void SetUpgradeSelection(int upgradeIndex) {
      if (_instance != null) {
        _instance._pendingUpgradeSelection = upgradeIndex;
      }
    }
  }
}