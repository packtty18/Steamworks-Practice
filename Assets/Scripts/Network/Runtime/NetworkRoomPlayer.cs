using Fusion;
using UnityEngine;

public sealed class NetworkRoomPlayer : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 4f;

    [Header("Visual")]
    [SerializeField]
    private Renderer targetRenderer;

    [SerializeField]
    private Color notReadyColor = Color.gray;

    [SerializeField]
    private Color readyColor = Color.green;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock _propertyBlock;
    private bool _lastAppliedReadyState;
    private bool _hasAppliedReadyState;

    [Networked]
    public bool IsReady { get; private set; }

    [Networked]
    private Vector3 NetworkedPosition { get; set; }

    public override void Spawned()
    {
        CacheRenderer();
        _propertyBlock = new MaterialPropertyBlock();

        if (Object.HasStateAuthority)
        {
            NetworkedPosition = transform.position;
        }

        ApplyNetworkedPosition();
        ApplyReadyColorIfNeeded(true);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        if (GetInput(out RoomPlayerInputData inputData) == false)
        {
            return;
        }

        Vector3 direction = inputData.CreateMovementDirection();

        if (direction == Vector3.zero)
        {
            return;
        }

        NetworkedPosition += direction * moveSpeed * Runner.DeltaTime;
        ApplyNetworkedPosition();
    }

    public override void Render()
    {
        ApplyNetworkedPosition();
        ApplyReadyColorIfNeeded(false);
    }

    public void SetReady(bool isReady)
    {
        if (Object == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            IsReady = isReady;
            return;
        }

        if (Object.HasInputAuthority)
        {
            RpcSetReady(isReady);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSetReady(bool isReady)
    {
        IsReady = isReady;
    }

    private void CacheRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void ApplyNetworkedPosition()
    {
        transform.position = NetworkedPosition;
    }

    private void ApplyReadyColorIfNeeded(bool force)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (force == false && _hasAppliedReadyState && _lastAppliedReadyState == IsReady)
        {
            return;
        }

        Color color = IsReady ? readyColor : notReadyColor;

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(BaseColorId, color);
        _propertyBlock.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(_propertyBlock);

        _lastAppliedReadyState = IsReady;
        _hasAppliedReadyState = true;
    }
}
