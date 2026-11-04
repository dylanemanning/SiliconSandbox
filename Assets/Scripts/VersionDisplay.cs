using UnityEngine;
using TMPro;

public class VersionDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text versionText;

    private void Start()
    {
        versionText.text = "v" + Application.version;
    }
}