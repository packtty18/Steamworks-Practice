using System;
using Fusion;
using UnityEngine;

public sealed class FusionRunnerFactory
{
    private readonly NetworkRunner _runnerPrefab;
    private readonly bool _provideInput;

    public FusionRunnerFactory(
        NetworkRunner runnerPrefab,
        bool provideInput)
    {
        _runnerPrefab = runnerPrefab;
        _provideInput = provideInput;
    }

    public NetworkRunner Create(INetworkRunnerCallbacks callbacks)
    {
        if (_runnerPrefab == null)
        {
            throw new InvalidOperationException("NetworkRunner 프리팹이 등록되지 않았습니다.");
        }

        NetworkRunner runner = UnityEngine.Object.Instantiate(_runnerPrefab);
        runner.name = $"NetworkRunner_{Guid.NewGuid():N}";
        runner.ProvideInput = _provideInput;
        runner.AddCallbacks(callbacks);

        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);

        return runner;
    }

    public void Destroy(NetworkRunner runner, INetworkRunnerCallbacks callbacks)
    {
        if (runner == null)
        {
            return;
        }

        runner.RemoveCallbacks(callbacks);
        UnityEngine.Object.Destroy(runner.gameObject);
    }
}
