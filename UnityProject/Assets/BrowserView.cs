﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class BrowserView : MonoBehaviour 
{
    
    public Button UpButton;
    public Button DownButton;
    public Button BackButton;
    public Button ForwardButton;
    public TMP_InputField UrlInputField;
    public TMP_Text ProgressText;
    public Transform ControllerForwardTransform;


    private RawImage _rawImage;
    private int _width = Screen.width;
    private int _androidScreenHeight = 1440; 
    private AndroidJavaObject _ajc;
    private Texture2D _imageTexture2D;

    private int _scrollByY;
    private bool _letLoadFirstUrl = true;
    private bool _shouldBeDrawingBrowser = true;

    
        
    // CALL THIS TO ADD KEYS TO BROWSER    
    public void AppendText(string appendText, bool isFunctionKey = false)
    {
        CallAjc("AddKeys", new object[]{appendText, isFunctionKey});
        Debug.Log("adding text to browser: " + appendText);
    }

    
    

    private void Awake()
    {
        UnityThread.initUnityThread();
    }

    private void Update()
    {
        OVRInput.Update();
        Vector3 fwd = ControllerForwardTransform.transform.forward;
        Debug.DrawRay(ControllerForwardTransform.transform.position, fwd * 50, Color.green);

        if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger))
        {
            if (Physics.Raycast(ControllerForwardTransform.transform.position, fwd, out var objectHit, 50))
            {
                Debug.Log("hit: " + objectHit.transform.name);
                //do something if hit object ie
                if(objectHit.transform==transform){
                    AddTap(objectHit.point);
                }
            }
        }        
    }

    private bool ValidHttpURL(string s, out Uri resultURI)
    {        
        bool returnVal = false;
        
        if (!Regex.IsMatch(s, @"^https?:\/\/", RegexOptions.IgnoreCase))
            s = "http://" + s;
        
        if (Uri.TryCreate(s, UriKind.Absolute, out resultURI))
            returnVal = (resultURI.Scheme == Uri.UriSchemeHttp || 
                         resultURI.Scheme == Uri.UriSchemeHttps);
        
        if (!s.Contains(".") || s.Contains(" "))
        {
            returnVal = false;
        }
        
        if (!Uri.IsWellFormedUriString(s, UriKind.Absolute)) 
            returnVal = false;   
        


        return returnVal;
    }

    public void InvokeStopLoading()
    {
        if (_ajc != null)
        {
            CallAjc("StopWebview",new object[]{});
        }
    }
    
    public void InvokeLoadURL()
    {
        if (UrlInputField.text == "")
        {
            LoadURL("google.com");
        }
        string potentialUrl = UrlInputField.text;
        if (ValidHttpURL(potentialUrl, out var outUri))
        {
            LoadURL(outUri.AbsoluteUri);    
        }
        else
        {
            string encodedSearchString = WebUtility.UrlEncode(potentialUrl);
            string searchUrl = "https://www.google.com/search?q=" + encodedSearchString;
            LoadURL(searchUrl);
        }
        
    }

    
    // Android to Java Methods:

    public void LoadURL(string url)
    {
        SetInputFieldUrl(url);
        if (_ajc != null)
        {            
            CallAjc("LoadURL",new object[]{ url});
        }
    }
    
    public void InvokeGoBack()
    {
        if (_ajc != null)
        {

            CallAjc("GoBack",new object[]{});
        }
    }
    
    public void InvokeGoForward()
    {
        if (_ajc != null)
        {

            CallAjc("GoForward",new object[]{});
        }
    }

    public void InvokeScrollUp()
    {
        CallAjc("Scroll",new object[]{ _scrollByY});
        Debug.Log("scrolling up!");

    }
    
    public void InvokeScrollDown()
    {
        CallAjc("Scroll",new object[]{-_scrollByY});
        Debug.Log("scrolling down!");

    }

    public void InsertText(string appendText)
    {
        CallAjc("AddKeys",new object[]{ appendText, false});

    }

    public void InvokeRefresh()
    {
        CallAjc("Refresh",new object[]{});
    }

    public void Backspace()
    {
        CallAjc("AddKeys", new object[]{"backspace", true});
        Debug.Log("going back");
    }
 
    public void MoveLeft()
    {
        CallAjc("AddKeys",new object[]{ "left", true});
        Debug.Log("going left");
    }

    public void MoveRight()
    {
        CallAjc("AddKeys", new object[]{"right", true});
//        _ajc.Call("AddKeys", "right", true);
        Debug.Log("going right");
     
    }

    // before calling anything to theplugin, make sure it has drawing enabled
    private void CallAjc(string methodName, object[] paramies)
    {
        if (_ajc != null)
        {
            _ajc.Call("SetShouldDraw", true);
            _ajc.Call(methodName,paramies);
        }
    } 

    // Android callback to change our browser view texture
    public void SetTexture( byte[] bytes, int width, int height, bool canGoBack, bool canGoForward)
    {
        // set can go back and forward interactability
        BackButton.interactable = canGoBack;
        ForwardButton.interactable = canGoForward;
        
        if (width != _imageTexture2D.width || height != _imageTexture2D.height)
            _imageTexture2D = new Texture2D(width, height, TextureFormat.ARGB32, false);
        
        _imageTexture2D.LoadImage(bytes);
        _imageTexture2D.Apply();
        _rawImage.texture = _imageTexture2D;
    }

    
    private string _lastUrl = "";
    public void SetInputFieldUrl(string url)
    {
        // don't set the url to the same thing multiple times, it may overwrite what the user has typed
        if (url == _lastUrl) return;
        if (url.Contains("www.youtube.com"))
            Debug.Log("We're sorry, but playing Youtube videos isn't yet supported.");
        UrlInputField.text = url;
    }

  
    
    // TODO: this may be a bad UX if we load a page then go home and expect to come back to it loaded
    // This makes sure the webview isn't drawing while this canvas isn't enabled
    private void OnCanvasEnableChanged(bool canvasEnabled)
    {
        if (_ajc == null || _letLoadFirstUrl) return; 
        _shouldBeDrawingBrowser = canvasEnabled;
        CallAjc("SetShouldDraw",new object[]{ _shouldBeDrawingBrowser});
    }


    public void ReloadPlugin()
    {
        if (_ajc != null)
        {
            _ajc.Call("RestartWebview");
            // we need to have the webview call draw so it can reset it self
            // as we cannot remove the dang view ourselves on this thread
            InvokeScrollDown();
            ProgressText.text = 0 + "%";
            StartCoroutine(LoadUrlLater());
        }

    }

    IEnumerator LoadUrlLater()
    {
        
        yield return new WaitForSeconds(1);
        InvokeLoadURL();
    }

  
    // Browser view must have pivot point at (0.5,0.5)
    void Start () {
        _imageTexture2D = new Texture2D(Screen.width, Screen.height, TextureFormat.ARGB32, false);
        _rawImage = gameObject.GetComponent<RawImage>();
        _rawImage.texture = _imageTexture2D;
 
        // working values but shows mobile
//        _width = (int) _rawImage.rectTransform.rect.width;
//        int outputHeight = (int) (_rawImage.rectTransform.rect.height);
        // testing new values 
        _width = 1200;//(int) _rawImage.rectTransform.rect.width;
        int outputHeight = 800;//(int) (_rawImage.rectTransform.rect.height);

        //        _androidScreenHeight = 1440; // this is the oculus go's height
        _androidScreenHeight = outputHeight;
        _scrollByY = (int) (_androidScreenHeight * .8 );
        
#if !UNITY_EDITOR && UNITY_ANDROID
        var tempAjc = new AndroidJavaClass("com.unityexport.ian.unitylibrary.MainGL");
        _ajc = tempAjc.CallStatic<AndroidJavaObject>("CreateInstance"); 
        // send the object to java to get frame update and keyboard display callbacks
        AndroidBitmapPluginCallback androidPluginCallback = new AndroidBitmapPluginCallback {BrowserView = this};
        _ajc.Call("SetUnityBitmapCallback", androidPluginCallback);
        // set output width and height
        _ajc.Call("SetOutputWindowSizes", _width, outputHeight);
        #endif
        _letLoadFirstUrl = true;
        LoadURL("https://www.google.com"); 

    }

    // method to to tap in the right coords despite difference in scaling
    private void AddTap(Vector3 pos)
    {


        RectTransform rectTransform = _rawImage.GetComponent<RectTransform>();

        // Bottom left and right positions will give us the width of the screen in the 3d world with
        // regards to the viewer
        Camera thisCamera = Camera.main;//gameObject.GetComponent<Camera>();
        Debug.Assert(thisCamera.name == "CenterEyeAnchor");
        Vector2 positionInRect = new Vector2();
        // transform the x=0 position by how far the browser's rect transform is from the center,
        // i.e. its position. 

        Vector2 screenPoint = RectTransformUtility .WorldToScreenPoint(thisCamera, pos);
        //Debug.Log("screen point: " + screenPoint);
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
            screenPoint, thisCamera, out positionInRect);
        //Debug.Log("main camera is: " + Camera.main.name);

        // take care of the pivots and their effect on position
        positionInRect.x += rectTransform.pivot.x * rectTransform.rect.width; 
        positionInRect.y += (rectTransform.pivot.y*rectTransform.rect.height);
        
        
        Debug.Assert(Math.Abs(rectTransform.pivot.y) > 0);
        // change coordinate system 
        positionInRect.y += -rectTransform.rect.height;
        positionInRect.y = Math.Abs(positionInRect.y);
        //Debug.Log(positionInRect);

        bool assumingPositionIsInWebView = true;
        // TODO: we're assuming the point is within the texture
        Debug.Assert(assumingPositionIsInWebView);
        // TODO: assuming these dimensions for oculus go screen
        // get the screen dimensions and divide them by the rectangle's screen dimensions for scaling
        float screenWidth = _width; //rect.width;
        float screenHeight = _androidScreenHeight; //rect.height;

        float xScale = screenWidth / rectTransform.rect.width; // rectWidthInScreen;
        float yScale = screenHeight / rectTransform.rect.height; // rectHeightInScreen;

        Vector2 positionInWebView = new Vector2(positionInRect.x * xScale, positionInRect.y * yScale);
        Debug.Log("position in webview: " + positionInWebView);
        // our scroll down is positive but the android's scroll down is negative
//        positionInWebView = new Vector2(positionInWebView.x, positionInWebView.y);

//        Debug.Log("transformed pos:" + positionInWebView);
        // if we're within the bounds of the rectangle
        if (_ajc!= null)
        {
            CallAjc("AddTap", new object[]{positionInWebView.x, positionInWebView.y});
        }
    }

    
    public void UpdateProgress(int progress, bool canGoBack, bool canGoForward)  {
        Debug.Log("progress is now:" + progress);
//        _loadingProgress = progress;
        if (progress >= 100)
        {
            _letLoadFirstUrl = false;
            UpButton.interactable = true;
            DownButton.interactable = true;
        }
        else
        {   
            UpButton.interactable = false;
            DownButton.interactable = false;
        }
        // if we're not finished loading the page, don't draw anything unless it's the first url

        BackButton.interactable = canGoBack;
        ForwardButton.interactable = canGoForward;
        ProgressText.text = progress + "%";
    }

   
}

// class used for the callback with the texture
class AndroidBitmapPluginCallback : AndroidJavaProxy
{
    public AndroidBitmapPluginCallback() : base("com.unityexport.ian.unitylibrary.PluginInterfaceBitmap") { }
    public BrowserView BrowserView;

    public void updateProgress(int progress, bool canGoBack, bool canGoForward)
    {
        UnityThread.executeInUpdate( () => BrowserView.UpdateProgress(progress, canGoBack,canGoForward)
        
        );
    }

    public void updateURL(string url)
    {
        Debug.Log("update url called! " + url);
        UnityThread.executeInUpdate( () => BrowserView.SetInputFieldUrl(url));
    }
    
    public void onFrameUpdate(AndroidJavaObject jo, int width, int height, bool canGoBack, bool canGoForward)
    {
        AndroidJavaObject bufferObject = jo.Get<AndroidJavaObject>("Buffer");
        byte[] bytes = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(bufferObject.GetRawObject());
//        Debug.Log("frame bytes arrived");
        //Debug.Log("bytes are "+bytes.Length + " long ");   
        if (bytes != null)
        {
            if (BrowserView != null)
            {
                UnityThread.executeInUpdate(()=> BrowserView.SetTexture(bytes,width,height,canGoBack,canGoForward));
//                Debug.Log("new height and width: " + height + " " + width);
            }
            else
                Debug.Log("TestAndroidPLugin is not set");
        }
    }

    // this doesn't really work
    public void SetKeyboardVisibility(string visibile)
    {
        Debug.Log("message from android about KEYBOARD visibility: " + visibile);

    }
    


}