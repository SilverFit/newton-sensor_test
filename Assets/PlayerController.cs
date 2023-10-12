using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private NewtonInputController inputController;
    void Start()
    {
        this.inputController = FindAnyObjectByType<NewtonInputController>();
    }

    void Update()
    {
        this.transform.position = new Vector3(0, Mathf.Lerp(-5, 5, this.inputController.GetPullAmount), 0);
    }
}
