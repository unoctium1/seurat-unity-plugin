using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableChildOnEnter : MonoBehaviour
{
    private GameObject child;

    private Collider col;
    private GameObject playerObj;

    // Start is called before the first frame update
    void Start()
    {
        child = transform.GetChild(0).gameObject;
        col = GetComponent<Collider>();
        playerObj = Camera.main.gameObject;
    }

    private void Update()
    {
        if (!child.activeInHierarchy && col.bounds.Contains(playerObj.transform.position)){
            child.SetActive(true);
        }
        else if(child.activeInHierarchy && !col.bounds.Contains(playerObj.transform.position))
        {
            child.SetActive(false);
        }
    }
}
