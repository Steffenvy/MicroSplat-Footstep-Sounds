﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Tester2 : MonoBehaviour
{
    public UnityEngine.UI.Text text;
    public FootSoundTester ts;

    private void Update()
    {
        text.text = ts.header;
    }
}
