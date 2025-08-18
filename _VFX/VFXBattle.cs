using CodeStage.AntiCheat.ObscuredTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Delegate
public delegate void AfterCollided(DmgResultData dmgResult);
#endregion

public class VFXBattle : VFX
{
    //----------------------------------------------------------------------------------------------------
    // ETC
    //----------------------------------------------------------------------------------------------------
    #region Delegate
    public Action OnCollide { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Property Fields
    public LayerMask TargetMask { get; protected set; }
    public BattleDB_Skill MyBattleDB { get; protected set; } = new();
    public List<Entity> ExceptedEntityList { get; protected set; } = new();
    public ObscuredBool OnStopUsing { get; protected set; }
    #endregion

    #region Protected Fields
    protected MyTimer HitDelayTimer { get; private set; }
    protected Coroutine ShowDamagedRoutine { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region SetUp Methods
    public void SetStopUsing() { OnStopUsing = true; }
    public virtual void SetUpUserOnly(Entity user) { MyBattleDB.user = user; }

    public virtual void SetUpStatus(BattleDB_Skill battleDB_Skill, LayerMask targetMask)
    {
        MyBattleDB.DeepCopy(battleDB_Skill);
        TargetMask = targetMask;

        SetUpDiedEvent();

        OnStopUsing = false;
    }

    public virtual void SetUpStatus(Entity user, AttackDB attackDB, int actionCount)
    {
        var instanceAbility = user.InstanceEntityAbility;
        MyBattleDB.DeepCopyUserInfo(user, user.CurEntityAbility);
        MyBattleDB.target = instanceAbility.targetEntityList[0];
        MyBattleDB.targetAbility = MyBattleDB.target.CurEntityAbility;
        MyBattleDB.curAttackDB = attackDB;
        MyBattleDB.CurActionCount = actionCount;

        TargetMask = instanceAbility.targetLayerMask;

        SetUpDiedEvent();

        OnStopUsing = false;
    }

    public virtual void SetUpStatus(Entity user, AttackDB attackDB, int actionCount, Vector2 position, Vector2 direction)
    {
        SetUpStatus(user, attackDB, actionCount);

        transform.TransformDirection(direction);

        MyBattleDB.usePosition = position;
        MyBattleDB.useDirection = direction;

        SetUpDiedEvent();

        OnStopUsing = false;
    }

    protected void SetUpDiedEvent()
    {
        if (MyBattleDB.cur_UserSkillDB != null)
        {

            MyBattleDB.user.EntityDied += Play_EndVFX;
            StageManager.Instance.ChangeWave += Play_EndVFX;
            MyBattleDB.cur_UserSkillDB.OnDismountSkill += Play_EndVFX;
        }

        else
        {
            MyBattleDB.user.EntityDied += SetStopUsing;
            StageManager.Instance.ChangeWave += SetStopUsing;
        }
    }

    private void RemoveDiedEvent()
    {
        if(MyBattleDB.user == null)
            return;

        if (MyBattleDB.cur_UserSkillDB != null)
        {
            if (MyBattleDB.user.EntityDied != null)
                MyBattleDB.user.EntityDied -= Play_EndVFX;

            if (StageManager.Instance.ChangeWave != null)
                StageManager.Instance.ChangeWave -= Play_EndVFX;

            if (MyBattleDB.cur_UserSkillDB.OnDismountSkill != null)
                MyBattleDB.cur_UserSkillDB.OnDismountSkill -= Play_EndVFX;
        }

        else
        {
            if (MyBattleDB.user.EntityDied != null)
                MyBattleDB.user.EntityDied -= SetStopUsing;

            if (StageManager.Instance.ChangeWave != null)
                StageManager.Instance.ChangeWave -= SetStopUsing;
        }
    }

    protected virtual void SetUpAoeToScale()
    {
        var _aoe = (float)MyBattleDB.cur_UserSkillDB.CommonAbilityGroups
            [MyBattleDB.commonAbilityIdx].commonAbilityDict[SkillCommonAbilityType.Aoe].GetAbilityValue(Ability_CalcValueType.Final);
        SetScaleRatio(_aoe);
    }
	#endregion

	#region Show Methods
	protected readonly WaitForSeconds _waitForTextSec = new WaitForSeconds(LocalDB_BattleEtc.multiHitDelayTime_Sec);
    protected WaitForSeconds _waitForCustomSec;
    protected IEnumerator ShowDamagedEvent(AfterCollided eventAction, DmgResultData dmgResult, float delayTime = 0.0f)
    {
        if(delayTime > 0.0f)
			_waitForCustomSec = new WaitForSeconds(delayTime);

		var returnValue = delayTime > 0.0f ? _waitForCustomSec : _waitForTextSec;

        for (var i = 0; i < dmgResult.hitCount; i++)
        {
            eventAction?.Invoke(dmgResult);
            yield return returnValue;
        }
    }
    #endregion

    #region Collision Methods
    protected virtual DmgResultData CollideEvent(Collider2D collider, Entity target, Vector2 hitCross) 
    {
        if (OnStopUsing)
            return new(1);

        if (!MyBattleDB.isTargeting || target != MyBattleDB.target)
			Update_CollideTarget(target);

		return CollisionDmg(target, hitCross);
	}

    protected void Update_CollideTarget(Entity _target)
    {
        MyBattleDB.target = _target;
        MyBattleDB.targetAbility = _target.CurEntityAbility;
    }

    private const int DEFAULT_HITCOUNT = 1;
    protected DmgResultData CollisionDmg(Entity target, Vector2 hitCross)
    {
        var dmgResult = MyBattleDB.cur_UserSkillDB != null 
            ? BattleCalculator.CalculateSkillDmg(MyBattleDB) 
            : BattleCalculator.CalculateDmg(MyBattleDB, DEFAULT_HITCOUNT);
        dmgResult.hitCross = hitCross;

        target.TakeDamage(ref dmgResult);
        OnCollide?.Invoke();

        if (target.CheckAliveAfterDmg())
        {
            ObscuredDouble finalDmg = 0.0;
            for (var i = 0; i < dmgResult.hitCount; i++)
            {
                if (dmgResult.finalDmgs[i] > finalDmg)
					finalDmg = dmgResult.finalDmgs[i];
			}

            if (finalDmg > 0.0)
            {
                MyBattleDB.CalcOnStrikingBuff();
                MyBattleDB.CalcOnStrikingAilment(finalDmg, target);
            }
        }

        return dmgResult;
    }

    protected virtual bool CheckCollisionTarget(Collider2D collision)
    {
        return !isDead && 1 << collision.gameObject.layer == TargetMask && !OnStopUsing && !onDiedAnimation;
    }

    protected virtual ObscuredBool CheckOnExceptedList(Entity collisionEntity)
    {
        if(collisionEntity.isDead || collisionEntity.CurEntityState == Entity.EntityState.Die)
            return true;

        ObscuredBool returnValue = false;
        foreach(var entity in ExceptedEntityList) 
        { 
            if(entity == collisionEntity)
            {
                returnValue = true;
                break;
            }
        }

        return returnValue;
    }
    #endregion

    #region Play Methods
    protected virtual void PlayCollisionVFX() { }

    protected virtual void PlayDeathVFX(Vector2 hitCross) { }

    public virtual void PlayEtcVfX(Entity target, DmgResultData dmg_ResultDB) { }

    public override void PlayEndVFX()
    {
        if (onDiedAnimation) return;

        RemoveDiedEvent();
        SetStopUsing();

        base.Play_EndVFX();
    }

    protected virtual void FuncAfterCollided(DmgResultData dmgResult) { }
    #endregion

    #region End Methods
    public override void KillForced(ObscuredBool isForced)
    {
        if(ShowDamagedRoutine != null)
        {
            StopCoroutine(ShowDamagedRoutine);
			ShowDamagedRoutine = null;
        }

        RemoveDiedEvent();
        OnCollide = null;

        if (ExceptedEntityList.Count > 0)
            ExceptedEntityList.Clear();

        MyBattleDB.Reset_All();

        base.Kill_Forced(isForced);
    }

    public virtual void FinishCharged()
    {
        if(MyBattleDB.user.CurEntityState == Entity.EntityState.Skill ||
            MyBattleDB.user.CurEntityState == Entity.EntityState.Attack ||
            MyBattleDB.user.CurEntityState == Entity.EntityState.Aim)
        {
            MyBattleDB.user.Finish_Charged();
        }

        else
            End_Die();
    }

    public virtual void FuncAfterCharged(ObscuredBool boolen) { MyAnimator.SetBool("IsCharged", boolen); }
    #endregion
}

