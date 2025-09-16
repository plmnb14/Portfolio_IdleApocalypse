//----------------------------------------------------------------------------------------------------
// 목적 : UI 오브젝트의 공통 부모 클래스
// 
// 주요 기능
// - IsActiveComponents로 On/Off 관리로 최적화 (GameObject 활성화 대신 필요한 부분만 On/Off)
// - 사용 컴포넌트 (RectTransform 등...) 캐싱 및 기본 세팅 자동화
// - 유지보수성과 최적화에 중점을 둔 UI 프레임워크의 기반 클래스
//----------------------------------------------------------------------------------------------------

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

