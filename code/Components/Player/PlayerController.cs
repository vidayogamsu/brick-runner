using System;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
using Sandbox.Services;

namespace Vidya;

public partial class PlayerController : Component, IGameEventHandler<PlayerRestart>
{
	public static PlayerController Instance { get; set; }

	public PlayerController()
	{
		Instance = this;
	}


	// [Property] public List<Clothing> Clothes { get; set; } = new();
	// public ClothingContainer Outfit { get; set; } = new();

	[Property] public CitizenAnimationHelper CAH { get; set; }
	[Property] public SkinnedModelRenderer Model { get; set; }

	[Property] public GameObject GibPrefab { get; set; }

	private static PlayerController _localPlayer;

	public static PlayerController Local
	{
		get
		{
			if ( !_localPlayer.IsValid() )
				_localPlayer = Game.ActiveScene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

			return _localPlayer;
		}
	}


	/// <summary> Gravity when not holding JUMP. </summary>
	[Property, Group( "Gravity" )] public float Gravity { get; set; } = 900f;
	/// <summary> Gravity when holding down the JUMP button. </summary>
	[Property, Group( "Gravity" )] public float GravityHeld { get; set; } = 450f;
	/// <summary> Normal vector pointing towards ground. Might change. </summary>
	[Property, Group( "Gravity" )] public Vector3 GravityDirection { get; set; } = Vector3.Down;
	public float ActiveGravity => HoldingJump ? GravityHeld : Gravity;

	[Property, Group( "Physics" )] public float GroundAccel { get; set; } = 10f;
	[Property, Group( "Physics" )] public float AirAccel { get; set; } = 5f;
	[Property, Group( "Physics" )] public float WalkSpeed { get; set; } = 150f;
	[Property, Group( "Physics" )] public float RunSpeed { get; set; } = 250f;
	[Property, Group( "Physics" )] public float Friction { get; set; } = 5f;

	[Property, Group( "Physics" )] public float JumpSpeed { get; set; } = 300f;

	public TimeSince LastGrounded { get; set; } = -1;

	/// <summary> Grace period of being airborne where player can still jump. </summary>
	[Property, Group( "Physics" )] public float CoyoteTime { get; set; } = 0.2f;
	/// <summary> If true: prevents coyote time. </summary>
	public bool CoyoteJumped { get; set; } = true;


	[Sync] public bool Dead { get; set; } = false;
	[Sync] public int Health { get; set; } = 4;
	[Sync] public int MaxLives { get; set; } = 4;


	// Input States
	public static bool PressedJump => Input.Pressed( "Jump" );
	public static bool HoldingJump => Input.Down( "Jump" );
	public static bool HoldingUp => Input.Down( "Up" );
	public static bool HoldingDown => Input.Down( "Down" );
	public static bool HoldingWalk => Input.Down( "Slow" );

	[Sync] public Vector3 SideDirection { get; set; }

	[Sync] public bool AbleToMove { get; set; } = true;

	[Sync] public Color Tint { get; set; } = "#FF0000";

	[Property, Sync] public List<GameObject> Renderers { get; set; } = new();

	[Property, Sync] public GameObject WorldPanelObject { get; set; }

	[Sync] public bool Paused { get; set; } = false;


	protected override void OnStart()
	{
		base.OnStart();

		Instance = this;

		/*foreach ( var wear in Clothes )
            if ( !Outfit.Has( wear ) )
                Outfit.Toggle( wear );

        Outfit.Apply( Model );*/
		if ( !IsProxy )
		{
			foreach ( var renderer in Components.GetAll<SkinnedModelRenderer>() )
			{
				Renderers.Add( renderer.GameObject );
			}

			WorldPanelObject = Components.GetAll<WorldPanel>().FirstOrDefault()?.GameObject;

			Scene.TimeScale = 1.0f;
		}
	}

	private Transform _lastTransform;

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			if ( Scene.TimeScale == 0f && Connection.All.Count > 1 )
			Scene.TimeScale = 1f;

			if ( Input.EscapePressed && !Paused )
			{
				Paused = true;

				if ( Connection.All.Count <= 1 )
					Scene.TimeScale = 0f;

				Input.EscapePressed = false;
			}
		}

		if ( Dead )
			return;

		UpdateBlink();

		// Citizen Animation
		CAH.IsGrounded = IsGrounded;

		CAH.WithVelocity( Velocity );

		if ( !SideDirection.y.AlmostEqual( 0f ) )
		{
			CAH.AimHeadWeight = 0f;
			CAH.Transform.Rotation = Rotation.LookAt( SideDirection );
		}

		if ( IsProxy )
			return;

		if ( !AbleToMove )
		{
			_lastTransform = Transform.World;
			Transform.World = _lastTransform;
			Transform.ClearInterpolation();
			Velocity = Vector3.Zero;
			return;
		}

		// Input
		SideDirection = new Vector3( 0f, MathF.Sign( -Input.AnalogMove.y ), 0f );
		var upDir = new Vector3( 0f, 0f, MathF.Sign( Input.AnalogMove.x ) );

		if ( HoldingUp && !HoldingDown ) upDir.z = 1f;
		else if ( HoldingDown && !HoldingUp ) upDir.z = -1f;

		// Movement
		var moveSpeed = HoldingWalk ? WalkSpeed : RunSpeed;

		if ( IsGrounded )
		{
			LastGrounded = 0;
			CoyoteJumped = false;

			// Jump
			if ( PressedJump )
			{
				Jump();
			}
			else
			{
				// Less friction when walking.
				ApplyFriction( HoldingWalk ? Friction * 0.5f : Friction );

				Velocity = Velocity.WithAcceleration( SideDirection * moveSpeed, GroundAccel * Time.Delta );
			}
		}
		else
		{
			var bCoyote = !CoyoteJumped && LastGrounded <= CoyoteTime;

			// Gravity
			if ( !bCoyote )
				Velocity += ActiveGravity * GravityDirection * Time.Delta;

			if ( PressedJump )
			{
				if ( bCoyote )
					Jump();
			}

			Velocity = Velocity.WithAcceleration( SideDirection * moveSpeed, AirAccel * Time.Delta );
		}

		// Custom Movement/Collision
		var (hHit, vHit) = Move( Velocity.y * Time.Delta, Velocity.z * Time.Delta );

		if ( IsGrounded && !WasGrounded && PreviousVelocity.z < 0f )
		{
			// Landing Sound
			Sound.Play( "player.land", Transform.Position );
		}

		// Nudge blocks when jumping.
		if ( vHit < 0f )
		{
			var trAll = TraceToAll( Transform.Position + Vector3.Up );

			// Hit the bricks, pal.
			foreach ( var tr in trAll )
			{
				if ( tr.Hit && tr.GameObject.IsValid() )
				{
					if ( tr.GameObject.Components.TryGet<BlockComponent>( out var block, FindMode.EverythingInSelfAndAncestors ) )
					{
						block.Nudge();

						var hitPos = Transform.Position + (Vector3.Up * Height * 0.9f);
						Sound.Play( "brick.hit", hitPos );
					}
				}
			}
		}

		// Clamp Position
		var pos = Transform.Position;

		if ( pos.z > 300f )
			SetPosition( pos.WithZ( 300f ) );

		// Death Pit
		if ( pos.z < -270f )
			Die();

		// if ( Input.Pressed( "attack1" ) )
		// Die();

		PreviousVelocity = Velocity;
		WasGrounded = IsGrounded;
	}

	public void Jump( bool playSound = true )
	{
		// Prevent further coyote time jumps.
		CoyoteJumped = true;

		BroadcastJumpAnim();

		if ( playSound )
			Sound.Play( "player.jump" );

		Velocity = Velocity.SubtractDirection( GravityDirection );
		Velocity += -GravityDirection * JumpSpeed;
		IsGrounded = false;
	}

	[Broadcast]
	void BroadcastJumpAnim()
	{
		CAH.TriggerJump();
	}


	/*
            Health
    */

	public TimeUntil BlinkingEnd { get; set; } = -1;
	public TimeUntil NextBlink { get; set; } = -1;
	public bool BlinkModel { get; set; } = false;

	[Property] public float BlinkDuration { get; set; } = 1.5f;
	public float BlinkCount { get; set; } = 12f;

	[Property] public CameraController CameraController { get; set; }

	[Broadcast]
	public void TakeDamage()
	{
		if ( Dead || !BlinkingEnd || !AbleToMove )
			return;

		if ( !IsProxy )
		{
			Health--;

			if ( Health <= 0 )
			{
				Die();
				return;
			}

			Sound.Play( "player.hurt" );

			StartBlinking();

			if ( GibPrefab.IsValid() )
			{
				var blood = GibPrefab.Clone( GameObject.GetBounds().Center );

				if ( blood.IsValid() && blood.Components.TryGet<GibSplosionComponent>( out var gib ) )
					gib.GibCount = 3;

				blood.NetworkSpawn();
			}
		}
	}

	[Broadcast]
	public void StartBlinking()
	{
		if ( BlinkDuration > 0 )
		{
			BlinkModel = true;
			BlinkingEnd = BlinkDuration;
			NextBlink = BlinkDuration / BlinkCount;
		}
	}

	[Broadcast]
	public void Die()
	{
		if ( Dead || !AbleToMove )
			return;

		var gs = GameSystem.Instance;

		if ( gs.IsValid() && !gs.OngoingGame )
			return;
		
		if ( WorldPanelObject.IsValid() )
			WorldPanelObject.Enabled = false;

		if ( Renderers is not null )
			foreach ( var model in Renderers )
				model.Enabled = false;

		// Log.Info( "Player died." );
		if ( !IsProxy )
			Dead = true;

		Sound.Play( "player.ded" );
		
		if ( GibPrefab.IsValid() )
		{
			var clone = GibPrefab.Clone( GameObject.GetBounds().Center );

			clone.NetworkSpawn();
		}

		if ( !IsProxy )
			Stats.Increment( "stat_deaths", 1 );

		if ( Scene.GetAllComponents<PlayerController>().Count( x => !x.Dead ) == 0 )
			GameSystem.Instance.EndGame();
	}

	public void UpdateBlink()
	{
		if ( BlinkingEnd )
		{
			BlinkModel = false;
		}
		else if ( NextBlink )
		{
			BlinkModel = !BlinkModel;
			NextBlink = BlinkDuration / BlinkCount;
		}

		Model.Tint = BlinkModel ? Color.White.WithAlpha( 0.3f ) : Color.White;

		foreach ( var renderer in Model.Components.GetAll<ModelRenderer>( FindMode.InDescendants ) )
		{
			renderer.Tint = BlinkModel ? Color.White.WithAlpha( 0.3f ) : Tint;
		}
	}


	/*
            Collision
    */

	[Property, Group( "Collision" )] public TagSet NoCollide { get; set; }
	[Property, Group( "Collision" )] public float Width { get; set; } = 9f;
	[Property, Group( "Collision" )] public float Height { get; set; } = 28f;

	[Sync] public bool IsGrounded { get; set; } = false;
	[Sync] public bool WasGrounded { get; set; } = false;

	[Sync] public Vector3 Velocity { get; set; } = Vector3.Zero;
	[Sync] public Vector3 PreviousVelocity { get; set; } = Vector3.Zero;


	private Vector3 _floatPos;

	/// <summary>
	/// Tracks fractional movement while clamping actual position to the grid.
	/// </summary>
	[Sync]
	public Vector3 FloatPos
	{
		get => _floatPos;
		set
		{
			if ( IsProxy )
				return;

			// Clamp actual position to grid.
			Transform.Position = new Vector3( 0f, MathF.Round( value.y ), MathF.Round( value.z ) );
			Transform.ClearInterpolation();

			// Store the sub-grid value for smooth movement.
			_floatPos = value;
		}
	}


	/// <summary>
	/// Use this instead of directly setting the transform's position.
	/// </summary>
	/// <param name="pos"></param>
	[Broadcast]
	public void SetPosition( Vector3 pos )
	{
		if ( IsProxy )
			return;

		FloatPos = pos;
	}

	/// <summary>
	/// Friction stolen from the default character controller.
	/// </summary>
	public void ApplyFriction( float frictionAmount, float stopSpeed = 140f )
	{
		float length = Velocity.Length;

		if ( !(length < 0.01f) )
		{
			float num = (length < stopSpeed) ? stopSpeed : length;
			float num2 = num * Time.Delta * frictionAmount;
			float num3 = length - num2;

			if ( num3 < 0f )
				num3 = 0f;

			if ( num3 != length )
			{
				num3 /= length;
				Velocity *= num3;
			}
		}
	}


	/// <summary>
	/// Move horizontally then vertically while respecting the grid.
	/// </summary>
	/// <param name="hMove">Horizontal movement.</param>
	/// <param name="vMove">Vertical movement.</param>
	public (float, float) Move( float hMove, float vMove )
	{
		var hHit = 0f;
		var vHit = 0f;

		// Horizontal Movement
		var hDest = FloatPos.y + hMove;
		var hDestGrid = MathF.Round( hDest );

		var trH = TraceTo( Transform.Position.WithY( hDestGrid ) );

		if ( trH.Hit )
		{
			var hDir = MathF.Sign( hMove );
			hHit = -hDir;

			// Snap to the wall and slightly off of it.
			FloatPos = trH.EndPosition.WithY( trH.EndPosition.y - hDir );
			Velocity = Velocity.WithY( 0f );
		}
		else
		{
			// Didn't hit a wall.
			FloatPos = FloatPos.WithY( hDest );
		}

		// Vertical Movement
		var vDest = FloatPos.z + vMove;
		var vDestGrid = MathF.Round( vDest );

		var trV = TraceTo( Transform.Position.WithZ( vDestGrid ) );

		if ( trV.Hit )
		{
			var vDir = MathF.Sign( vMove );
			vHit = -vDir;

			// Snap to the floor/ceiling and slightly off of it.
			FloatPos = trV.EndPosition.WithZ( trV.EndPosition.z - vDir );
			Velocity = Velocity.WithZ( 0f );
		}
		else
		{
			// Didn't hit a floor/ceiling.
			FloatPos = FloatPos.WithZ( vDest );
		}

		// Check if we're on the ground.
		if ( Velocity.z <= 0f )
		{
			var trGround = TraceTo( Transform.Position + (Vector3.Down * 1f) );

			IsGrounded = trGround.Hit || trGround.StartedSolid;

			if ( IsGrounded )
				Velocity = Velocity.WithZ( 0f );
		}

		return (hHit, vHit);
	}

	public BBox BoundingBox => BBox.FromHeightAndRadius( Height, Width );

	public SceneTraceResult TraceTo( Vector3 to )
	{
		return Scene.Trace.Box( BoundingBox, Transform.Position, to )
			.WithoutTags( NoCollide )
			.Run();
	}

	public IEnumerable<SceneTraceResult> TraceToAll( Vector3 to )
	{
		return Scene.Trace.Box( BoundingBox, Transform.Position, to )
			.WithoutTags( NoCollide )
			.RunAll();
	}

	void IGameEventHandler<PlayerRestart>.OnGameEvent( PlayerRestart eventArgs )
	{
		// Restore Health
		Health = MaxLives;
		Dead = false;
		// Safe Teleport
		Velocity = Vector3.Zero;
		PreviousVelocity = Vector3.Zero;
		// funky collision protection
		StartBlinking();
		// Reset Position
		SetPosition( eventArgs.Pos );

		if ( CameraController.IsValid() )
		{
			CameraController.Spectating = false;
			CameraController.SpectateTarget = null;
		}

		AbleToMove = true;

		if ( !IsProxy )
			BroadcastPlayerRestart();
	}

	[Broadcast]
	public void BroadcastPlayerRestart()
	{
		if ( Renderers is not null )
			foreach ( var model in Renderers )
				model.Enabled = true;
		
		if ( WorldPanelObject.IsValid() )
			WorldPanelObject.Enabled = true;
	}
}
