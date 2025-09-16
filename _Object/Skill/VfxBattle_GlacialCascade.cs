//----------------------------------------------------------------------------------------------------
// 목적 : "빙하 폭포" 스킬의 본체 VFS (VfxBattleMine_GC 발동 이후 생성되어, 실제로 충돌/피해/후처리 등을 담당함)
// 
// 주요 기능
// - VFXBattle 상속 받아, 충돌 피해 계산, 연출 함수 재사용
// - MyTimer로 종료 후(오브젝트 생존 이후), KillForced로 반환 처리
// - BattleDB 복사본을 통해 타깃/공격 파라미터 일관성 유지
// - 설치형 스킬(Mine) 피해/연출 예시
//----------------------------------------------------------------------------------------------------

using CodeStage.AntiCheat.ObscuredTypes;
using UnityEngine;

public class VfxBattle_GlacialCascade : VFXBattle
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Properties
    public MyTimer VfxTimer { get; set; }
    #endregion

    #region Protected Fields
    protected ObscuredFloat _maxAliveTimeSec = 0.25f;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Collision Event
    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (CheckCollisionTarget(collider))
            return;

        collider.TryGetComponent(out Entity collidedTarget);
        if (CheckOnExceptedList(collidedTarget))
            return;

        PlayDeathSound();

        var hitCross = collider.ClosestPoint(transform.position);
        var dmgResult = CollideEvent(collider, collidedTarget, hitCross);
        PlayDeathVFX(hitCross);
        PlayEtcVfX(collidedTarget, dmgResult);
    }
    #endregion

    #region SetUp Methods
    public override void SetUpStatus(BattleDB_Skill battleDB_Base, LayerMask targetMask)
    {
        base.SetUpStatus(battleDB_Base, targetMask);

        VfxTimer = TimeManager.Instance.GetTimer();
        VfxTimer.UpdateTime(_maxAliveTimeSec);

        var sfxTyp = ActionVFXType.Main;
        var sfxName = MyBattleDB.CurAttackDB.SfxNames[(int)sfxTyp][0];
        SoundManager.Instance.PlayEffectAudio(sfxName);
    }
    #endregion

    #region End Methods
    public override void KillForced(ObscuredBool isForced)
    {
        if (VfxTimer != null)
        {
            TimeManager.Instance.BackTimer(VfxTimer);
            VfxTimer = null;
        }

        base.KillForced(isForced);
    }
    #endregion

    #region Play Methods
    public override void Play_BeginVFX()
    {
        if(VfxTimer != null)
        {
            VfxTimer.StartTimer(VfxTimer.RemainingSeconds);
            VfxTimer.OnTimerEnd += Play_EndVFX;
        }

        else
            Play_EndVFX();
    }

    protected override void PlayDeathVFX(Vector2 hitCross)
    {
        var vfx = VFXManager.Instance.GetVFX(MyBattleDB.CurAttackDB.VfxNames[(int)ActionVFXType.Hit][0], 
            hitCross, BackgroundManager.Instance.GetScrolledWorldTransform());
    }

    public override void PlayEndVFX()
    {
        if (onDiedAnimation) return;

        base.PlayEndVFX();

        if (VfxTimer != null)
        {
            TimeManager.Instance.BackTimer(VfxTimer);
            VfxTimer = null;
        }

        var sfxTyp = ActionVFXType.Extra;
        var sfxName = MyBattleDB.CurAttackDB.SfxNames[(int)sfxTyp][1];
        SoundManager.Instance.PlayEffectAudio(sfxName);
    }

    public override void PlayEtcVfX(Entity target, DmgResultData dmg_ResultDB)
    {
        if (OnStopUsing)
            return;

        target.CheckAndMoveBackward(DmgType.Normal, LocalDB_Skill.SKILL_KNOCKBACK * ScaleRatio);
    }

    protected override void PlayDeathSound()
    {
        var sfxTyp = ActionVFXType.Hit;
        var sfxName = MyBattleDB.CurAttackDB.SfxNames[(int)sfxTyp][0];
        SoundManager.Instance.PlayEffectAudio(sfxName);
    }

    public override void CamShakeOnAnimation()
    {
        CamShakeOnScript(0.3f, 0.3f);
    }
    #endregion
}



