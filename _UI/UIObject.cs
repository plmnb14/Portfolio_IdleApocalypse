using UnityEngine;
using UnityEngine.UI;
using CodeStage.AntiCheat.ObscuredTypes;

public abstract class UIObject : MonoBehaviour
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Space]
    [Header(" - UI 오브젝트 ( UI Object )")]
    [SerializeField] protected Image[] etcImages;
    #endregion

    #region Property Fields
    public RectTransform MyRectTransform 
    {
        get { return myRectTransform; }
        protected set { myRectTransform = value; }
    }
    private RectTransform myRectTransform;

    public ObscuredBool IsActiveComponents { get; set; } = true;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    protected virtual void SetUpOnAwake() 
    {
        TryGetComponent(out RectTransform _tempRectTransform);
        MyRectTransform = _tempRectTransform;
    }
    #endregion

    #region Update Methods
    public virtual void UpdateUI() { }
    public virtual void UpdateLanguageText() { }
    #endregion

    #region Activate Methods
    public virtual void ActivateObject(ObscuredBool isActive) 
    {
        if(IsActiveComponents != isActive)
            IsActiveComponents = isActive;

        if (etcImages != null)
            MyOptimization.EnableImages(etcImages, isActive);
    }
    #endregion

    #region Die Methods
    public virtual void BeginDie() { }
    public virtual void KillForced() { }
    public virtual void ResetStatus() { }
    #endregion
}
