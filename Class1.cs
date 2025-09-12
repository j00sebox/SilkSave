using BepInEx;
using UnityEngine;

[BepInPlugin("com.example.helloworld", "HelloWorld Mod", "1.0.0")]
public class HelloWorld : BaseUnityPlugin
{
    void Start()
    {
        Logger.LogInfo("Hello, Silksong from VS Code!");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Logger.LogInfo("F5 was pressed!");
        }
    }
}