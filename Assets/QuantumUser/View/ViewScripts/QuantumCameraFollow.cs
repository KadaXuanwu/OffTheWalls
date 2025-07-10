using Quantum;
using UnityEngine;

public class QuantumCameraFollow : QuantumEntityViewComponent<CustomViewContext> {
    public Vector2 offset;

    private bool _local;
    private Vector3 _lastPosition;

    private const float CameraZOffset = -10f;
    private const float CameraSmoothSpeed = 10f;
    private const float CameraLookAheadDistance = 0.9f;
    private const int PredictionAreaRadius = 10;

    public override void OnActivate(Frame frame) {
        base.OnActivate(frame);

        var link = frame.Get<PlayerLink>(EntityRef);
        _local = Game.PlayerIsLocal(link.Player);

        _lastPosition = transform.position;
    }

    public override void OnUpdateView() {
        base.OnUpdateView();

        if (_local == false) {
            return;
        }

        Vector3 currentPosition = transform.position;

        /*
        Vector3 cameraOffset = new(offset.x, offset.y, CameraZOffset);
        Vector3 desiredPosition = currentPosition + cameraOffset;

        Vector3 smoothedPosition = Vector3.Lerp(ViewContext.MyCamera.transform.position, desiredPosition, CameraSmoothSpeed * Time.deltaTime);
        ViewContext.MyCamera.transform.position = smoothedPosition;
        */

        ViewContext.MyCamera.transform.position = new(currentPosition.x + offset.x,
                                                      currentPosition.y + offset.y,
                                                      CameraZOffset);

        Game.SetPredictionArea(currentPosition.ToRoundedFPVector2(), PredictionAreaRadius);
    }
}
