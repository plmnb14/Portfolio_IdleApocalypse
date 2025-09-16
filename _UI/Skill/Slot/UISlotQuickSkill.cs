//----------------------------------------------------------------------------------------------------
// 목적 : 스킬 퀵슬롯 UI (수동/자동 아이콘 표시, 쿨다운/프리셋 인덱스 노출, 만료/완료 시 하이라이트 처리)
// 
// 주요 기능
// - 뒤끝(Backend) SDK 연동 및 서버 빌드 전환(개발/라이브)
// - 앱 버전 동기화 및 강제 업데이트 처리 로직 구현
// - 광고(AdMob Mediation), FireBase 등 외부 서비스 초기화 및 관리
// - 메인 스레드 큐를 활용한 안전한 콜백 처리
//----------------------------------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using CodeStage.AntiCheat.ObscuredTypes;

public class UISlotQuickSkill : UISlotSkillBase, IPointerClickHandler
{
    //----------------------------------------------------------------------------------------------------
    // ETC
    //----------------------------------------------------------------------------------------------------
    #region Delegate
    public PressButtonIndex OnPressedSlot { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Space]
    [Header("스킬 퀵 슬롯 (Skill Quick Slot)")]
    [SerializeField] protected UISkillCooldownGroup skillCooldownGroupUI;
    [SerializeField] protected Image highlightIcon;
    [SerializeField] protected Image manualPressHighLightImg;
    [SerializeField] protected bool updateCooldownUI;
    [SerializeField] protected UITextWithBackground corePresetIdxUI;
    #endregion

    #region Property Fields
    public ObscuredBool IsAutoMode { get; set; } = true;
    public ObscuredBool IsForcedUpdateCooldown { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Interface Methods
    //----------------------------------------------------------------------------------------------------
    #region Click Methods
    public void OnPointerClick(PointerEventData eventData)
    {
        OnPressedSlot?.Invoke(slotIndex);
    }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        SetUp_OnAwake();
    }

    protected override void SetUpOnAwake()
    {
        base.SetUpOnAwake();

        isUpdateImages = false;
        slotIcon.sprite = null;
        highlightIcon.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        if(manualPressHighLightImg != null)
            manualPressHighLightImg.enabled = false;

        if(corePresetIdxUI != null)
        {
            corePresetIdxUI.SetDigit(0);
            corePresetIdxUI.ActivateObject(false);
        }
    }
    #endregion

    #region Update Methods
    public override void UpdateUI()
    {
        base.UpdateUI();

        if(updateCooldownUI)
        {
            ObscuredBool isCooldown = SkillItem != null 
                                ? SkillItem.SkillDB_Cur.IsCooldown 
                                ? true 
                                : false 
                                : false;

            skillCooldownGroupUI.ActivateObject(isCooldown);
        }
    }

    public void UpdateOriginCoreStonePresetUI(ObscuredInt presetIdx)
    {
        if(presetIdx == -1)
            corePresetIdxUI.ActivateObject(false);

        else if (!isEmpty && corePresetIdxUI != null)
        {
            corePresetIdxUI.ActivateObject(true);
            corePresetIdxUI.Set_ValueText((SkillItem.SkillDB_Save.QuickSlotPresets[(int)LocalDB_Skill.SkillPresetType.CoreStone, presetIdx] + 1).ToString("N0"));
        }
    }

    public void UpdateCoreStonePresetUI(ObscuredInt presetIdx)
    {
        if (presetIdx == -1)
            corePresetIdxUI.ActivateObject(false);

        else if (!isEmpty && corePresetIdxUI != null)
        {
            corePresetIdxUI.ActivateObject(true);
            corePresetIdxUI.Set_ValueText((presetIdx + 1).ToString("N0"));
        }
    }
    #endregion

    #region Activate Skill Methods
    public void StartSkill(Hero user)
    {
        SkillItem.StartSkill(user);
    }

    public void StartSkillEffect(BattleDB_Skill battleDBSkill, ObscuredBool isRepeating)
    {
        SkillItem.StartSkillEffect(battleDBSkill, isRepeating);

        if(!isRepeating)
        {
            SkillItem.SkillDB_Cur.IsCooldown = true;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            skillCooldownGroupUI.ActivateObject(true);
            skillCooldownGroupUI.SetCooldownValue(battleDBSkill.cur_UserSkillDB.CurCooldown, battleDBSkill.cur_UserSkillDB.MaxCooldown);
            cooldownCoroutine = StartCoroutine(WaitSkillCooldown());
        }
    }
    #endregion

    #region CoolTime Methods
    private Coroutine cooldownCoroutine;
    public void UpdateOnlyCooldown(SkillDB_Cur curSkillDB)
    {
        if (curSkillDB.CurCooldown <= 0.0 || !curSkillDB.IsCooldown)
            return;

        UpdateOnlyCooldownTime(curSkillDB);

        if (cooldownCoroutine == null)
            cooldownCoroutine = StartCoroutine(WaitSkillCooldown());
    }

    public void UpdateOnlyCooldownTime(SkillDB_Cur curSkillDB)
    {
        if(!skillCooldownGroupUI.IsActivateComponenets)
            skillCooldownGroupUI.ActivateObject(true);

        skillCooldownGroupUI.SetCooldownValue(curSkillDB.CurCooldown, curSkillDB.MaxCooldown);
    }

    public void ResetOnlyCooldown() { ResetCoolTime(); }


    private const float COOLDOWN_TIME_TICK = 0.1f;
    private IEnumerator WaitSkillCooldown()
    {
        while (SkillItem != null 
            && SkillItem.SkillDB_Cur != null 
            && SkillItem.SkillDB_Cur.IsCooldown 
            && SkillItem.SkillDB_Cur.CurCooldown > 0.0f)
        {            
            SkillItem.SkillDB_Cur.CurCooldown -= COOLDOWN_TIME_TICK;

            if(SkillItem.SkillDB_Cur.CurCooldown < LocalDB_Ability.abilityEpsilon)
                SkillItem.SkillDB_Cur.CurCooldown = 0.0f;

            skillCooldownGroupUI.UpdateCooldownValue(SkillItem.SkillDB_Cur.CurCooldown);

            ObscuredFloat elapsedTime = 0.0f;

            while (elapsedTime < COOLDOWN_TIME_TICK)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        if(SkillItem != null && SkillItem.SkillDB_Cur != null && SkillItem.SkillDB_Cur.IsCooldown)
        {
            if (SkillItem.SkillDB_Cur.CurCooldown <= LocalDB_Ability.abilityEpsilon)
                ResetCoolTime();
        }

        else
            skillCooldownGroupUI.SetCooldownValue(0.0f, 0.0f);

        skillCooldownGroupUI.ActivateObject(false);
        cooldownCoroutine = null;
    }

    private void ResetCoolTime()
    {
        SkillItem.SkillDB_Cur.IsCooldown = false;
        SkillItem.SkillDB_Cur.CurCooldown = 0.0f;
        skillCooldownGroupUI.SetCooldownValue(0.0f, 0.0f);
        HighlightSlot();
    }

    private void HighlightSlot()
    {
        highlightIcon.color = Color.white;
        highlightIcon.DOFade(0.0f, 0.3f).SetEase(Ease.OutExpo);
    }
    #endregion

    #region Get Methods
    public UISkillCooldownGroup GetCooldownUI() { return skillCooldownGroupUI; }
    #endregion

    #region Unlock Methods
    protected override void UnlockUIObject(ObscuredBool isUnlock)
    {
        base.UnlockUIObject(isUnlock);

        slotBackground.raycastTarget = isUnlock;
    }
    #endregion

    #region Activate Methods
    public override void ActivateObject(ObscuredBool isActive)
    {
        base.ActivateObject(isActive);

        if (!isActive)
            skillCooldownGroupUI.ActivateObject(false);

        if(corePresetIdxUI != null && !isActive)
            corePresetIdxUI.ActivateObject(isActive);
    }
    #endregion
}






