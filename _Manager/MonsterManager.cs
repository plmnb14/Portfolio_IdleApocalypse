using CodeStage.AntiCheat.ObscuredTypes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterManager : Singleton<MonsterManager>
{
    //----------------------------------------------------------------------------------------------------
    // delegate
    //----------------------------------------------------------------------------------------------------
    #region Delegate
    public delegate void Update_EnemyCount(ObscuredInt enemyCount);
    public Update_EnemyCount OnUpdatedEnemyCount { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Header(" - 몬스터 소환 범위")]
    [SerializeField] private Vector2 monsterSummonYRange = new();
    [SerializeField] private float summonDistanceX = 2.0f;
    #endregion

    #region Consts Fields
    private readonly AbilityType_Entity[] updateAbilityTypes = new AbilityType_Entity[]
        {
            AbilityType_Entity.AtkPoint_Base, AbilityType_Entity.DefPoint_Base, AbilityType_Entity.LifePoint_Base,
            AbilityType_Entity.MovementSpd_Mul, AbilityType_Entity.Gold_Obtain, AbilityType_Entity.Exp_Obtain
        };
    #endregion

    #region Private Fields
    private readonly Dictionary<ObscuredString, Queue<Monster>> _poolingMonsterDict = new();
    private readonly Dictionary<ObscuredString, Monster> _prefabDict = new();
    private readonly List<Monster> _instanceMonsterList = new();
    private ObscuredInt _createCnt = 20;
    private ObscuredInt _currentSummonCnt = 0;
    private ObscuredBool _isBossSummoned = false;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        if(CheckOverlap())
            DontDestroy();
    }
    #endregion

    #region Check Manager
    public ObscuredBool CheckEnemyRemain()
    {
        return _instanceMonsterList.Count > 0;
    }
    #endregion

    #region Object Managemement Methods
    public Monster GetMonster(ObscuredString monsterName)
    {
        var monster =
            _poolingMonsterDict[monsterName].Count > 0 ?
            _poolingMonsterDict[monsterName].Dequeue() : AddEnemy(monsterName);

        monster.ResetTransform(Vector2.zero);
        monster.ResetStatus();
        monster.transform.SetParent(BackgroundManager.Instance.GetScrolledWorldTransform());
        monster.SetUpBeforeActive();
        monster.ActivateObject(true);

        return monster;
    }

    public void BackMonster(ObscuredString key, Monster monster, ObscuredBool isForced)
    {
        InsertEnemy(key, monster);

        if (!isForced)
        {
            _instanceMonsterList.Remove(monster);
            OnUpdatedEnemyCount?.Invoke(_instanceMonsterList.Count);

            if(_currentSummonCnt > 0)
                --_currentSummonCnt;

            if (0 >= _currentSummonCnt && !CheckEnemyRemain())
            {
                _instanceMonsterList.Clear();
                if (!_isBossSummoned)
                    _isBossSummoned = false;

                if(!StageManager.Instance.IsChangingMap)
                    StageManager.Instance.Clear_CurrentStage(StageClearType.Win);
            }
        }
    }

    private void InsertEnemy(ObscuredString monsterName, Monster monster)
    {
        if(_poolingMonsterDict.ContainsKey(monsterName))
        {
            monster.ActivateObject(false);
            monster.transform.SetParent(transform);
            _poolingMonsterDict[monsterName].Enqueue(monster);
        }

        else
            Destroy(monster);
    }

    private Monster AddEnemy(ObscuredString key)
    {
        var enemyDBServer = DataManager.Instance.Enemy_InfoDBDict[key];

        Monster monster = Instantiate(_prefabDict[key]);
        monster.enemyDB_Server = enemyDBServer;
        monster.myName = key;

        InsertEnemy(key, monster);

        return _poolingMonsterDict[key].Dequeue();
    }

    private const string MONSTER_PREFAB_PATH = "_Build Prefabs/Entity/{0}";
    private void CreateEnemy(ObscuredString key)
    {
        if(_prefabDict.ContainsKey(key))
            return;

        var enemyDBServer = DataManager.Instance.Enemy_InfoDBDict[key];
        var monsterPrefab = Resources.Load<Monster>(string.Format(MONSTER_PREFAB_PATH, enemyDBServer.prefabPath));
        _prefabDict.Add(key, monsterPrefab);

        _poolingMonsterDict.Add(key, new());
        for (var i = 0; i < _createCnt; i++)
        {
            Monster monster = Instantiate(monsterPrefab);

            monster.enemyDB_Server = enemyDBServer;
            monster.myName = key;

            InsertEnemy(key, monster);
        }
    }

    private void DestroyEnemy()
    {
        _instanceMonsterList.Clear();

        foreach (var keyValue in _poolingMonsterDict)
        {
            while(keyValue.Value.Count > 0)
                Destroy(keyValue.Value.Dequeue());

            keyValue.Value.Clear();
        }

        _poolingMonsterDict.Clear();
    }

    private const string CARVING_DUNGEON_BASE_ID = "cp6000";
    public void CreateEnemyFromDB(ChapterDB_Server chapterDB, StageDB_Server stageDB)
    {
        var curChapEnemyNames = chapterDB.EnemyNames;
        if (chapterDB.itemID == CARVING_DUNGEON_BASE_ID)
        {
            var basedChapterDB = DataManager.Instance.Chapter_InfoDBDict[stageDB.baseChapterId];
            curChapEnemyNames = basedChapterDB.EnemyNames;
        }

        var loopCnt = curChapEnemyNames.Length;
        for(var i = 0; i < loopCnt; i++)
            CreateEnemy(curChapEnemyNames[i]);
    }
    #endregion

    #region Monster Management Methods
    private Coroutine _summonCoroutine;
    public void ReadyToSummonEnemy(StageDB_Server stageDB)
    {
        if (SaveDataManager.Instance.ErrorForcedPauseGame)
            return;

        _waitForSeconds = new WaitForSeconds(stageDB.summonDelays[StageManager.Instance.CurWaveIdx]);

        StopSummonEnemy();
		_summonCoroutine = StartCoroutine(SummonEnemy(stageDB));
    }

    public void StopSummonEnemy()
    {
        if (_summonCoroutine == null) 
            return;

		StopCoroutine(_summonCoroutine);
		_summonCoroutine = null;
	}

    private WaitForSeconds _waitForSeconds;
    public IEnumerator SummonEnemy(StageDB_Server stageDB)
    {
        _currentSummonCnt = 0;

        // 현재 웨이브 인덱스
        ObscuredInt waveIndex = StageManager.Instance.CurWaveIdx;

        // 현재 웨이브에 소환될 적 타입 개수
        ObscuredInt waveEnemyCnt = stageDB.enemy_NamesPerWave[waveIndex].Length;

        // 현재 몬스터 최대 개수
        ObscuredInt maxEnemyCnt = StageManager.Instance.CurStageDB.GetWaveMaxEnemyCnt(waveIndex);
        _currentSummonCnt = maxEnemyCnt;

        // 몬스터 HP바 설정해야됨
        var hpBarMgr = SliderBarUIManager.Instance;
        var stageMgr = StageManager.Instance;
        for (var i = 0; i < waveEnemyCnt; i++)
        {
            if (SaveDataManager.Instance.ErrorForcedPauseGame)
            {
                StageManager.Instance.PauseStageProgress();
                yield break;
            }

            if (stageMgr.IsChangingMap)
                yield break;

            var summonCnt = stageDB.enemySummon_Cnts[waveIndex][i];
            var enemyName = stageDB.enemy_NamesPerWave[waveIndex][i];
            var enemyType = stageDB.enemyGrade_Types[waveIndex][i];
            var enemyLevel = stageDB.EnemyLevel;
            var enemyPresetType = stageDB.enemyPreset_Types[waveIndex][i];
            var enemyAbilityPrefixes = stageDB.enemyAbilityPrefix_Types[waveIndex][i];

            var monsterPrefab = DataManager.Instance.Enemy_InfoDBDict[enemyName];
            var curPresetType = enemyPresetType == EnemyPresetType.End ? monsterPrefab.presetType : enemyPresetType;

            for(var j = 0; j < summonCnt; j++)
            {
                if (stageMgr.IsChangingMap)
                    yield break;

                // 몬스터 불러오기
                Monster monster = GetMonster(enemyName);

                // 기본 설정
                monster.CurEntityGradeType = enemyType;
                SetMonsterAbility(monster, enemyLevel, stageDB);

                // 노말, 보스 차이
                ObscuredFloat scaleRatio = 1.0f;
                ObscuredFloat lifeRatio = 1.0f;
                ObscuredFloat randomY;
                UIHpBar hpBar;

                if (enemyType == EntityGradeType.Boss)
                {
                    randomY = stageMgr.PlayerSummonPosition.y;

                    hpBar = hpBarMgr.GetBossHPBarUI(false);
                    var bossHPBarUI = hpBar as UIHpBarBoss;
                    ObscuredInt _languageIdx = SettingManager.GetLanguangeIndex();
                    bossHPBarUI.SetBossNameText(monster.enemyDB_Server.itemNames[_languageIdx]);
                    monster.EntityDied += () => { bossHPBarUI.isTimerActivated = false; };

                    var sliderModeType = UIHpBarBoss.SliderMode.Default;
                    if(stageMgr.CurChapterDB.chapterType == ChapterType.Raid)
                    {
                        sliderModeType = UIHpBarBoss.SliderMode.Raid;
                        monster.InstanceEntityAbility.IsInvincible = true;
                    }

                    // 일반 스테이지/ 던전의 보스
                    else
                    {
                        scaleRatio = LocalDB_Enemy.BOSS_SCALE_RATIO;
                        lifeRatio = LocalDB_Enemy.BOSS_LIFE_RATIO;
						monster.InstanceEntityAbility.IsInvincible = false;
                    }

                    bossHPBarUI.Cur_SliderMode = sliderModeType;
                }

                else
                {
                    hpBar = hpBarMgr.GetMonsterHPBarUI();

                    if (enemyType == EntityGradeType.Elite)
                    {
                        scaleRatio = LocalDB_Enemy.ELITE_SCALE_RATIO;
                        lifeRatio = LocalDB_Enemy.ELITE_LIFE_RATIO;
						monster.InstanceEntityAbility.IsInvincible = false;

                        randomY = stageMgr.PlayerSummonPosition.y;
                    }

                    else
                        randomY = Random.Range(monsterSummonYRange.x, monsterSummonYRange.y);
                }

                if (stageMgr.CurChapterDB.itemID == LocalDB_Map.RAID_MAP_BASE_ID)
                {
					scaleRatio = LocalDB_Enemy.BOSS_SCALE_RATIO;
					lifeRatio = LocalDB_Enemy.BOSS_LIFE_RATIO;
					monster.InstanceEntityAbility.IsInvincible = false;

                    randomY = stageMgr.PlayerSummonPosition.y;
                }

                // 배율 설정
                monster.SetScaleRatio(scaleRatio);
                monster.InstanceEntityAbility.lifeRatio = lifeRatio;

                // 인스턴스 능력치 설정
                monster.InstanceEntityAbility.SetAbilityValue(
                    AbilityType_Instance.Life,
                    monster.CurEntityAbility[AbilityType_Entity.LifePoint_Final]);

                // 위치 설정
                monster.transform.position = new Vector3(summonDistanceX, randomY, 0.0f);

                // HP바 설정
                monster.SetUpHpBar(hpBar);
                if (enemyType == EntityGradeType.Boss || enemyType == EntityGradeType.Elite)
                {
                    monster.ActivateHpBar();
                    SetUpAiblityPrefix(monster, stageDB, enemyAbilityPrefixes);
                }

                monster.StartSummoning();
                monster.StartForceSummon();

                // 인스턴스 리스트에 소환
                _instanceMonsterList.Add(monster);

                OnUpdatedEnemyCount?.Invoke(_instanceMonsterList.Count);

                yield return _waitForSeconds;
            }
        }

        yield break;
    }

    private void SetMonsterAbility(Monster monster, ObscuredDouble enemylevel, StageDB_Server stageDB)
    {
        monster.CurEntityAbility.Reset_Default(true);
        monster.CurEntityAbility.ValueChangedDict[AbilityType_Entity.Atk_Spd_Mul] += monster.Update_AttackSpeed;
        monster.CurEntityAbility.ValueChangedDict[AbilityType_Entity.MovementSpd_Mul] += monster.Update_MoveSpeed;
        monster.CurEntityAbility.SetAbilityValue(AbilityType_Entity.Level, enemylevel.GetDecrypted(), Ability_CalcValueType.Base);

        var presetData = DataManager.Instance.Enemy_PresetDBDict[monster.enemyDB_Server.presetType];

        var conertMonsterLv = monster.CurEntityAbility[AbilityType_Entity.Level] - 1;
        if(conertMonsterLv < 1) 
            conertMonsterLv = 1;

        var baseTypeInt = (int)ValueType.Base;
        var perTypeInt = (int)ValueType.Per;
        foreach (var ability in presetData.baseAbilityDict)
        {
            var baseValue = ability.Value[baseTypeInt].GetAbilityValue();
            var perValue = ability.Value[perTypeInt].GetAbilityValue() * conertMonsterLv;

            monster.CurEntityAbility.SetAbilityValue(ability.Key, baseValue, Ability_CalcValueType.Base);
            monster.CurEntityAbility.SetAbilityValue(ability.Key, perValue, Ability_CalcValueType.Add);
            monster.CurEntityAbility.UpdateAbilityFinalValue(ability.Key);
        }

        foreach(var abilityRatio in stageDB.abilityRatioDict)
        {
            var calcType = abilityRatio.Key == AbilityType_Entity.Def_Ignore ? Ability_CalcValueType.Add : Ability_CalcValueType.Mul;
            monster.CurEntityAbility.SetAbilityValue(abilityRatio.Key, (ObscuredDouble)abilityRatio.Value, calcType);
            monster.CurEntityAbility.UpdateAbilityFinalValue(abilityRatio.Key);
        }

        monster.Update_AttackSpeed(monster.CurEntityAbility[AbilityType_Entity.Atk_Spd_Mul]);
        monster.Update_MoveSpeed(monster.CurEntityAbility[AbilityType_Entity.MovementSpd_Mul]);
    }

    private void SetUpAiblityPrefix(Monster monster, StageDB_Server stageDB, EnemyAbilityPrefix[] prefixAbilityTypes)
    {
        if (monster.CurEntityGradeType != EntityGradeType.Elite || prefixAbilityTypes == null)
            return;

		var prefixAbilityCnt = prefixAbilityTypes.Length;
		for (var i = 0; i < prefixAbilityCnt; i++)
		{
			var curPrefixType = prefixAbilityTypes[i];
			monster.HpBar.Add_AbilityPrefix(curPrefixType);

			AbilityType_Entity entityAbilityType = AbilityType_Entity.End;
			ObscuredDouble prefixValue = 0.0;

			switch (curPrefixType)
			{
				case EnemyAbilityPrefix.Offensive:
					{
						entityAbilityType = AbilityType_Entity.AtkPoint_Mul;
						prefixValue = LocalDB_Enemy.enemyAbilityPrefixDict[curPrefixType].value;
                        
						break;
					}

				case EnemyAbilityPrefix.Defensive:
					{
						entityAbilityType = AbilityType_Entity.DefPoint_Mul;
						prefixValue = LocalDB_Enemy.enemyAbilityPrefixDict[curPrefixType].value;

						break;
					}

				case EnemyAbilityPrefix.Healthy:
					{
						entityAbilityType = AbilityType_Entity.LifePoint_Mul;
						prefixValue = LocalDB_Enemy.enemyAbilityPrefixDict[curPrefixType].value;

						break;
					}
				//----------------------------------------------------------------------------------------------------
				case EnemyAbilityPrefix.Haste:
					{
						entityAbilityType = AbilityType_Entity.MovementSpd_Mul;
						prefixValue = LocalDB_Enemy.enemyAbilityPrefixDict[EnemyAbilityPrefix.Haste].value;

						SetUpAbilityAgain(monster, stageDB, AbilityType_Entity.Atk_Spd_Mul, prefixValue);

						break;
					}

				case EnemyAbilityPrefix.Maximum:
				case EnemyAbilityPrefix.Minimum:
					{
						var _scaleRatio = (float)LocalDB_Enemy.enemyAbilityPrefixDict[curPrefixType].value.GetDecrypted();

						monster.transform.localScale = Vector3.one;
						monster.SetScaleRatio(_scaleRatio);
						break;
					}

				case EnemyAbilityPrefix.SuperArmor:
					{
						monster.InstanceEntityAbility.IsSuperArmor = true;
						break;
					}
			}

			if (entityAbilityType != AbilityType_Entity.End && prefixValue > 0.0)
				SetUpAbilityAgain(monster, stageDB, entityAbilityType, prefixValue);

			if (curPrefixType == EnemyAbilityPrefix.Haste)
			{
				monster.Update_AttackSpeed(monster.CurEntityAbility[AbilityType_Entity.Atk_Spd_Mul] * prefixValue);
				monster.Update_MoveSpeed(monster.CurEntityAbility[AbilityType_Entity.MovementSpd_Mul] * prefixValue);
			}
		}
	}

    private void SetUpAbilityAgain(Monster monster, StageDB_Server stageDB, AbilityType_Entity abilityType, ObscuredDouble prefixValue)
    {
        var ratio = stageDB.abilityRatioDict.ContainsKey(abilityType) ? stageDB.abilityRatioDict[abilityType].GetDecrypted() : 1.0f;
        var finalValue = prefixValue * ratio;

        var calcType = abilityType == AbilityType_Entity.Def_Ignore ? Ability_CalcValueType.Add : Ability_CalcValueType.Mul;
        monster.CurEntityAbility.SetAbilityValue(abilityType, finalValue, calcType);
        monster.CurEntityAbility.UpdateAbilityFinalValue(abilityType);
    }
    #endregion

    #region Check Methods
    public List<Entity> CheckEnemyInRange(Transform userTransform, ObscuredFloat rangeValue)
    {
        var userPositionX = userTransform.position.x;
        var range = rangeValue;

        List<Entity> detectedMonsterList = new();
        foreach (var monster in _instanceMonsterList)
        {
            if(monster.isDead || monster.CurEntityState == Entity.EntityState.Die)
                continue;

            var targetX = monster.Get_LockOnPosition().x;
            var distnace = Mathf.Abs(userPositionX - targetX);

            if (distnace <= range)
				detectedMonsterList.Add(monster);
        }

        if(detectedMonsterList.Count > 1)
        {
			detectedMonsterList.Sort((Entity a, Entity b) =>
            {
                var distnaceA = Mathf.Abs(userPositionX - a.Get_LockOnPosition().x);
                var distnaceB = Mathf.Abs(userPositionX - b.Get_LockOnPosition().x);

                return distnaceA.CompareTo(distnaceB);
            });
        }

        return detectedMonsterList;
    }
    #endregion

    #region Reset Methods
    public void ResetInstanceEnemy()
    {
        foreach (var monster in _instanceMonsterList)
            monster.KillForced(true);

		_instanceMonsterList.Clear();
		_isBossSummoned = false;
        _currentSummonCnt = 0;
    }

    public void PauseInstanceEnemy()
    {
        foreach (var monster in _instanceMonsterList)
        {
            if(!monster.isDead)
                monster.PauseActing();
        }
    }
    #endregion
}