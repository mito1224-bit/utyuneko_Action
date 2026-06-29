using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioBank", menuName = "Audio/Audio Bank")]
public class AudioBank : ScriptableObject
{
    [Header("このバンクに所属するSEのリスト")]
    public List<SoundManager.SeData> seList = new List<SoundManager.SeData>();
}