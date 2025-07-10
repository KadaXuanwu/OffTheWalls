namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.InputSystem;

  public class QuantumDebugInput : MonoBehaviour {
    [SerializeField] private PlayerInput playerInput;

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

      Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
      Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0));
      i.MousePosition = new FPVector2(mouseWorldPos.x.ToFP(), mouseWorldPos.y.ToFP());

      callback.SetInput(i, DeterministicInputFlags.Repeatable);
    }
  }
}