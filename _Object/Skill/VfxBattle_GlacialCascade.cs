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

    public override void Play_EndVFX()
    {
        if (onDiedAnimation) return;

        base.Play_EndVFX();

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
