using System.Collections.Generic;
using UnityEngine;
using System;
using BackEnd;
using CodeStage.AntiCheat.ObscuredTypes;

public enum UnitDigitType
{
    None,
    man, uk, jo, kyong, ja, yang, gu, gan, jung, jae, guk, a,
    End
};

public enum ItemCalcType
{
    Add, Sub, 
    End
}

public delegate void OnUpdateCurrencyValue(ObscuredDouble value);

public class CurrencyManager : Singleton<CurrencyManager>
{
    //----------------------------------------------------------------------------------------------------
    // Etc
    //----------------------------------------------------------------------------------------------------
    #region Delegate
    public delegate void OnUpdateCurrencyValue(CurrencyType type, ObscuredDouble value);
    public OnUpdateCurrencyValue CurrencyValue_Delegate { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Const Fields
    private const int PREVIEW_CURRENCY_CNT = 2;
    #endregion

    #region Serialize Fields
    [Space]
    [Header("   - 재화 매니저 (Currency Manager)")]
    [SerializeField] private UICurrency[] currencyUIs = new UICurrency[PREVIEW_CURRENCY_CNT];
    #endregion

    #region Private Fields
    private HashSet<CurrencyType> PreviewCurrenyHashSet { get; set; } = new();
    #endregion

    #region Server Save DB Fields
    public UserInfo_GameSaveData DefaultUserInfo_SaveData { get; private set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Data SetUp Methods
    //----------------------------------------------------------------------------------------------------
    #region Data SetUp Methods
    public void SetUpSaveData(GameSaveData gameSaveData)
    {
        DefaultUserInfo_SaveData = gameSaveData as UserInfo_GameSaveData;
    }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        if(CheckOverlap())
        {
            DontDestroy();
            SetUpOnAwake();
        }
    }

    private void SetUpOnAwake()
    {
        currencyUIs[0].ShowCurrencyType = CurrencyType.Gold;
        currencyUIs[1].ShowCurrencyType = CurrencyType.Crystal;

        for (int i = 0; i < PREVIEW_CURRENCY_CNT; i++)
            PreviewCurrenyHashSet.Add(currencyUIs[i].ShowCurrencyType);
    }
    #endregion

    #region Start Methods
    private void Start()
    {
        SetUpOnStart();
    }

    private void SetUpOnStart()
    {
        foreach(var type in PreviewCurrenyHashSet)
            UpdateCurrencyText(type);
    }
    #endregion

    #region String/Text Methods
    public void UpdateCurrencyText(CurrencyType currencyType)
    {
        var stringValue = Generate_CurrencyText(currencyType);
        currencyUIs[currencyType == CurrencyType.Gold ? 0 : 1].UpdateCurrencyText(stringValue);
    }

    public ObscuredString Generate_CurrencyText(CurrencyType type)
    {
        var currency = DefaultUserInfo_SaveData.CurrencyDict[type];
        var unitType = (int)currency.currencyUnit;
        var value = Math.Truncate((currency.Get_ItemCnt(ItemCntType.Final) * LocalDB_Currency.unitValueDemical[unitType]) * 100) * 0.01;

        return string.Format("{0}{1}", value.ToString(unitType < 1 ? "N0" : "#.##"), LocalDB_Currency.unitString[unitType]);
    }

    public ObscuredString Generate_CurrencyText(CurrencyItemBase currency)
    {
        var unitType = (int)currency.currencyUnit;
        ObscuredDouble _value = Math.Truncate((currency.Get_ItemCnt(ItemCntType.Final) * LocalDB_Currency.unitValueDemical[unitType]) * 100) * 0.01;

        return string.Format("{0}{1}", _value.ToString(unitType < 1 ? "N0" : "#.##"), LocalDB_Currency.unitString[unitType]);
    }

    // 숫자 크기에 따라 자릿수 나눠주고 단위 붙여주는 함수.
    // 외부에서도 숫자를 넣으면 문자열로 바꿔주게끔 해둠
    public ObscuredString GenerateNumberText(ObscuredDouble value)
    {
        ObscuredBool isMinusValue = false;

        if(value < 0.0)
        {
            isMinusValue = true;
            value *= -1.0f;
        }

        ObscuredInt currentNumberUnit = 0;
        const int maxDigitCnt = (int)UnitDigitType.End + 1;
        for (int i = 1; i < maxDigitCnt; i++)
        {
            if (value < LocalDB_Currency.currencyUnitValueInteger[i])
            {
                currentNumberUnit = i - 1;
                break;
            }
        }
        ObscuredDouble finalValue = Math.Truncate((value * LocalDB_Currency.unitValueDemical[currentNumberUnit]) * 1000) * 0.001;

        return string.Format(isMinusValue ? "-{0}{1}" : "{0}{1}", finalValue.ToString(currentNumberUnit < 1 ? "N0" : "#.##"), LocalDB_Currency.unitString[currentNumberUnit]);
    }

    public ObscuredString GenerateNumberText(ObscuredLong value)
    {
        ObscuredBool isMinusValue = false;

        if (value < 0.0)
        {
            isMinusValue = true;
            value *= -1;
        }

        ObscuredInt currentNumberUnit = 0;
        const int maxDigitCnt = (int)UnitDigitType.End + 1;
        for (int i = 1; i < maxDigitCnt; i++)
        {
            if (value < LocalDB_Currency.currencyUnitValueInteger[i])
            {
                currentNumberUnit = i - 1;
                break;
            }
        }
        ObscuredDouble finalValue = Math.Truncate((value * LocalDB_Currency.unitValueDemical[currentNumberUnit]) * 1000) * 0.001;

        return string.Format(isMinusValue ? "-{0}{1}" : "{0}{1}", finalValue.ToString(currentNumberUnit < 1 ? "N0" : "N2"), LocalDB_Currency.unitString[currentNumberUnit]);
    }

    public ObscuredString GenerateNumberTextDoubleCollection(ObscuredDouble value, string unitCnt, ObscuredBool changeColor, ObscuredBool bold, ObscuredBool suffixDigit)
    {
        ObscuredInt currentNumberUnit = 0;
        const int maxDigitCnt = (int)UnitDigitType.End + 1;
        for (int i = 1; i < maxDigitCnt; i++)
        {
            if (value < LocalDB_Currency.currencyUnitValueInteger[i])
            {
                currentNumberUnit = i - 1;
                break;
            }
        }

        ObscuredDouble finalValue = Math.Truncate((value * LocalDB_Currency.unitValueDemical[currentNumberUnit]) * 100000) * 0.00001;

        string finalString = string.Empty;
        if (suffixDigit)
        {
            finalString = changeColor ?
                bold ? TextColor.GetDefaultMonotoneCode(TextColor.monotoneLightIdx,
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit]))) :

            TextColor.GetDefaultMonotoneCode(TextColor.monotoneLightIdx,
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit]))) :

            string.Format("{0}",
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit])));
        }

        else
        {
            finalString = changeColor ?
                bold ? string.Format("<color=#E6E6E6><b>{0}{1}</b></color>",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]) :

            string.Format("<color=#E6E6E6>{0}{1}</color>",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]) :

            string.Format("{0}{1}",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]);
        }

        return finalString;
    }

    public ObscuredString GenerateNumberText(ObscuredDouble value, string unitCnt, ObscuredBool changeColor, ObscuredBool bold, ObscuredBool suffixDigit)
    {
        ObscuredInt currentNumberUnit = 0;
        const int maxDigitCnt = (int)UnitDigitType.End + 1;
        for (int i = 1; i < maxDigitCnt; i++)
        {
            if (value < LocalDB_Currency.currencyUnitValueInteger[i])
            {
                currentNumberUnit = i - 1;
                break;
            }
        }

        ObscuredDouble finalValue = Math.Truncate((value * LocalDB_Currency.unitValueDemical[currentNumberUnit]) * 1000) * 0.001;

        string finalString = string.Empty;
        if (suffixDigit)
        {
            finalString = changeColor ?
                bold ? TextColor.GetDefaultMonotoneCode(TextColor.monotoneLightIdx,
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit]))) :

            TextColor.GetDefaultMonotoneCode(TextColor.monotoneLightIdx,
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit]))) :
            
            string.Format("{0}",
                finalValue.ToString(string.Format(unitCnt, LocalDB_Currency.unitString[currentNumberUnit])));
        }

        else
        {
            finalString = changeColor ?
                bold ? string.Format("<color=#E6E6E6><b>{0}{1}</b></color>",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]) :

            string.Format("<color=#E6E6E6>{0}{1}</color>",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]) :

            string.Format("{0}{1}",
                finalValue.ToString(unitCnt),
                LocalDB_Currency.unitString[currentNumberUnit]);
        }

        return finalString;
    }

    public ObscuredString Generate_CashNumberText(ObscuredLong cashValue)
    {
        var finalString = cashValue == 0 ? ConstTexts.FREE_TEXT : cashValue.ToString(ConstDigit.digits[0][0]);
        var finalSubfix = finalString != ConstTexts.FREE_TEXT ? string.Format("{0}{1}", finalString, ConstTexts.WON_TEXT) : finalString;
        return finalSubfix;
    }
    #endregion

    #region Currency Get/Set/Add Methods
    public ObscuredDouble GetCurrency(CurrencyType currencyType, ItemCntType countType = ItemCntType.Final)
    {
        return DefaultUserInfo_SaveData.CurrencyDict[currencyType].Get_ItemCnt(countType);
    }

    public ObscuredBool CalcCurrency(CurrencyType currencyType, ObscuredDouble count, ItemCalcType itemCalcType, ItemCntType countType)
    {
        var calcResult = DefaultUserInfo_SaveData.CurrencyDict[currencyType].Calc_ItemCnt(count, itemCalcType, countType);

        if (calcResult && count != 0)
        {
            DefaultUserInfo_SaveData.ForcedUpdateDataChange(true);
            if (CheckTypeOnList(currencyType))
            {
                UpdateCurrencyText(currencyType);
            }

            if (itemCalcType == ItemCalcType.Sub)
            {
                switch (currencyType)
                {
                    case CurrencyType.Gold:
                    case CurrencyType.Crystal:
                    case CurrencyType.VoidStone:
                    case CurrencyType.Ether:
                        {
                            var convertedInt =
                                currencyType == CurrencyType.Gold ? REQ_Currency.UseGold :
                                currencyType == CurrencyType.Crystal ? REQ_Currency.UseCrystal :
                                currencyType == CurrencyType.VoidStone ? REQ_Currency.UseVoidStone : REQ_Currency.UseEther;

                            AchievementManager.Instance.AddAccureRequireCnt(
                                REQ_Type.Currency,
                                (int)convertedInt,
                                (long)count);

                            break;
                        }
                }
            }

            else if(itemCalcType == ItemCalcType.Add)
            {
                switch (currencyType)
                {
                    case CurrencyType.EquipmentTicket:
                        {
                            GambleManager.Instance.UpdateGambleMenuUI(GambleType.Weapon);
                            break;
                        }
                    case CurrencyType.SkillTicket:
                    case CurrencyType.CoreStoneTicket:
                        {
                            GambleManager.Instance.UpdateGambleMenuUI(GambleType.Skill);
                            break;
                        }
                    case CurrencyType.ArtifactTicket:
                        {
                            GambleManager.Instance.UpdateGambleMenuUI(GambleType.Artifact);
                            break;
                        }
                }
            }

            var finalCurrencyCnt = DefaultUserInfo_SaveData.CurrencyDict[currencyType].Get_ItemCnt(ItemCntType.Final).GetDecrypted();
            CurrencyValue_Delegate?.Invoke(currencyType, finalCurrencyCnt);
        }

        return calcResult;
    }

    private ObscuredBool CheckTypeOnList(CurrencyType currencyType)
    {
        for (int i = 0; i < PREVIEW_CURRENCY_CNT; i++)
        {
            if (PreviewCurrenyHashSet.Contains(currencyType))
                return true;
        }

        return false;
    }
    #endregion

    #region Check Methods
    public ObscuredBool CheckCurrencyRequired(CurrencyType currencyType, ObscuredLong cost)
    {
        return GetCurrency(currencyType) >= cost;
    }
    #endregion
}
