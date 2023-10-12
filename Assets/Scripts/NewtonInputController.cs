using SilverFit.Newton.Common.Input;
using SilverFit.Newton.Interface;
using UnityEngine;

public class NewtonInputController : MonoBehaviour
{
    private InputManager inputManager;

    private void Awake()
    {
        RomSettings romSettings = new RomSettings();
        NewtonSettings newtonSettings = new NewtonSettings();

        this.inputManager = new InputManager(romSettings, newtonSettings, false);
        this.inputManager.Initialize();
    }

    void Update()
    {
        inputManager.Update();
        Debug.Log($"{this.inputManager.GetPullAmount()}");
    }

    public float GetPullAmount => this.inputManager.GetPullAmount();

    private void OnDestroy()
    {
        inputManager.Destroy();
    }
}