using Godot;
using System;
using System.Collections.Generic;

public sealed class GameArena
{
	private const float PlayerSpeed = 265f;
	private const float ArenaMargin = 38f;
	private const int MaxEnemies = 90;
	private const float EnemyHpScalePerMinute = 1.25f;
	private const float SwarmGrowthPerNinetySeconds = .01f;

	private readonly Node2D _owner;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Enemy> _enemies = new();
	private readonly List<Bullet> _bullets = new();
	private readonly List<Gem> _gems = new();
	private readonly List<Spark> _sparks = new();

	private Vector2 _player;
	private Vector2 _aimDirection = Vector2.Right;
	private float _elapsed;
	private float _shootTimer;
	private float _spawnTimer;
	private float _hp = 100f;
	private float _maxHp = 100f;
	private float _levelFlash;
	private int _xp;
	private int _level = 1;
	private int _nextLevel = 8;
	private int _kills;
	private float _playerDamage = 1f;
	private float _shotArc = 0f;
	private bool _gameOver;

	public GameArena(Node2D owner)
	{
		_owner = owner;
	}

	public void Initialize()
	{
		_rng.Randomize();
		_player = _owner.GetViewportRect().Size * 0.5f;

		for (int i = 0; i < 12; i++)
		{
			SpawnEnemy(initial: true);
		}

		_owner.QueueRedraw();
	}

	public void Update(float delta)
	{
		if (_gameOver)
		{
			if (Input.IsKeyPressed(Key.R))
			{
				ResetGame();
			}

			_owner.QueueRedraw();
			return;
		}

		_elapsed += delta;
		_levelFlash = Mathf.Max(0f, _levelFlash - delta);

		MovePlayer(delta);
		UpdateSpawns(delta);
		UpdateCombat(delta);
		UpdateEnemies(delta);
		UpdateBullets(delta);
		UpdateGems(delta);
		UpdateSparks(delta);

		_owner.QueueRedraw();
	}

	public void Draw()
	{
		Vector2 size = _owner.GetViewportRect().Size;
		_owner.DrawRect(new Rect2(Vector2.Zero, size), new Color("#090d18"));

		for (int x = 0; x < size.X; x += 48)
		{
			_owner.DrawLine(new Vector2(x, 78), new Vector2(x, size.Y), new Color(.10f, .14f, .23f, .42f));
		}

		for (int y = 96; y < size.Y; y += 48)
		{
			_owner.DrawLine(new Vector2(0, y), new Vector2(size.X, y), new Color(.10f, .14f, .23f, .42f));
		}

		_owner.DrawRect(new Rect2(0, 0, size.X, 78), new Color("#111a2d"));
		Font font = ThemeDB.FallbackFont;

		_owner.DrawString(font, new Vector2(24, 34), "NIGHT SHIFT", HorizontalAlignment.Left, -1, 24, new Color("#f4f0df"));
		_owner.DrawString(font, new Vector2(24, 59), "SURVIVE THE SWARM", HorizontalAlignment.Left, -1, 12, new Color("#7f8ca8"));
		_owner.DrawString(font, new Vector2(24, 74), $"FPS {Engine.GetFramesPerSecond():000}", HorizontalAlignment.Left, -1, 10, new Color("#6f7d9b"));
		_owner.DrawString(font, new Vector2(size.X - 300, 32), "WASD / ARROWS   MOUSE AIM", HorizontalAlignment.Left, -1, 13, new Color("#aab5cd"));
		_owner.DrawString(font, new Vector2(size.X - 265, 57), $"TIME  {_elapsed:000.0}", HorizontalAlignment.Left, -1, 16, new Color("#f4f0df"));

		foreach (Gem gem in _gems)
		{
			if (gem.IsGolden)
			{
				_owner.DrawCircle(gem.Pos, 11f, new Color(1f, .67f, .12f, .18f));
				_owner.DrawCircle(gem.Pos, 7f, new Color("#ffc928"));
				_owner.DrawCircle(gem.Pos, 3f, new Color("#fff4a6"));
				continue;
			}

			_owner.DrawColoredPolygon(
				new[]
				{
					gem.Pos + new Vector2(0, -7),
					gem.Pos + new Vector2(7, 0),
					gem.Pos + new Vector2(0, 7),
					gem.Pos + new Vector2(-7, 0)
				},
				new Color("#6fe7c8"));
		}

		// A short guide and bright point make the exact firing direction readable
		// without hiding enemies or covering the mouse cursor.
		Vector2 aimPoint = _player + _aimDirection * 74f;
		_owner.DrawLine(_player + _aimDirection * 21f, _player + _aimDirection * 62f, new Color(1f, .83f, .38f, .5f), 2f);
		_owner.DrawCircle(aimPoint, 8f, new Color(1f, .78f, .28f, .12f));
		_owner.DrawArc(aimPoint, 8f, 0f, Mathf.Tau, 24, new Color("#fff3a6"), 2f);
		_owner.DrawCircle(aimPoint, 3f, new Color("#fff3a6"));

		foreach (Bullet bullet in _bullets)
		{
			_owner.DrawCircle(bullet.Pos, 3f, new Color("#fff3a6"));
			_owner.DrawCircle(bullet.Pos, 6f, new Color(1f, .75f, .25f, .14f));
		}

		foreach (Enemy enemy in _enemies)
		{
			float bob = Mathf.Sin(enemy.Phase) * 2f;
			Vector2 pos = enemy.Pos + new Vector2(0, bob);

			_owner.DrawCircle(pos, 14f, new Color("#321e3d"));
			_owner.DrawCircle(pos, 10f, new Color("#d94c69"));
			_owner.DrawCircle(pos + new Vector2(-4, -2), 2f, new Color("#ffe29a"));
			_owner.DrawCircle(pos + new Vector2(4, -2), 2f, new Color("#ffe29a"));
		}

		foreach (Spark spark in _sparks)
		{
			_owner.DrawCircle(spark.Pos, 3f, new Color(spark.Color, Mathf.Clamp(spark.Life * 3f, 0f, 1f)));
		}

		_owner.DrawCircle(_player + new Vector2(0, 5), 20f, new Color(0, 0, 0, .35f));
		_owner.DrawCircle(_player, 18f, new Color("#273b72"));
		_owner.DrawCircle(_player, 13f, new Color("#65b8ff"));
		_owner.DrawCircle(_player + new Vector2(-4, -4), 4f, new Color("#e8f5ff"));

		_owner.DrawRect(new Rect2(24, size.Y - 40, 210, 12), new Color("#202941"));
		_owner.DrawRect(new Rect2(24, size.Y - 40, 210f * _hp / _maxHp, 12), new Color("#e95b73"));
		_owner.DrawString(font, new Vector2(24, size.Y - 49), $"HP {_hp:000} / {_maxHp:000}", HorizontalAlignment.Left, -1, 12, new Color("#dfe7f4"));
		_owner.DrawString(font, new Vector2(size.X - 210, size.Y - 47), $"LVL {_level:00}     KILLS {_kills:000}", HorizontalAlignment.Left, -1, 15, new Color("#dfe7f4"));

		_owner.DrawRect(new Rect2(size.X * .5f - 130, size.Y - 25, 260, 9), new Color("#202941"));
		_owner.DrawRect(new Rect2(size.X * .5f - 130, size.Y - 25, 260f * _xp / _nextLevel, 9), new Color("#6fe7c8"));

		if (_levelFlash > 0f)
		{
			_owner.DrawString(font, new Vector2(size.X * .5f - 110, 145), "LEVEL UP  + MAX HP", HorizontalAlignment.Left, -1, 22, new Color("#6fe7c8"));
		}

		if (_gameOver)
		{
			_owner.DrawRect(new Rect2(0, 0, size.X, size.Y), new Color(.02f, .03f, .07f, .72f));
			_owner.DrawString(font, new Vector2(size.X * .5f - 115, size.Y * .5f - 18), "YOU GOT SWARMED", HorizontalAlignment.Left, -1, 25, new Color("#ff6b7f"));
			_owner.DrawString(font, new Vector2(size.X * .5f - 95, size.Y * .5f + 25), "PRESS R TO RISE AGAIN", HorizontalAlignment.Left, -1, 14, new Color("#f4f0df"));
		}
	}

	private void MovePlayer(float delta)
	{
		Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

		if (Input.IsKeyPressed(Key.A)) input.X -= 1f;
		if (Input.IsKeyPressed(Key.D)) input.X += 1f;
		if (Input.IsKeyPressed(Key.W)) input.Y -= 1f;
		if (Input.IsKeyPressed(Key.S)) input.Y += 1f;

		if (input.Length() > 1f)
		{
			input = input.Normalized();
		}

		_player += input * PlayerSpeed * delta;

		Vector2 viewportSize = _owner.GetViewportRect().Size;
		_player.X = Mathf.Clamp(_player.X, ArenaMargin, viewportSize.X - ArenaMargin);
		_player.Y = Mathf.Clamp(_player.Y, 100f, viewportSize.Y - ArenaMargin);
	}

	private void UpdateSpawns(float delta)
	{
		_spawnTimer -= delta;

		int enemyCap = GetEnemyCap();
		if (_spawnTimer <= 0f && _enemies.Count < enemyCap)
		{
			SpawnEnemy(initial: false);
			_spawnTimer = Mathf.Max(.16f, (.72f - _elapsed * .006f) / Mathf.Pow(1.01f, Mathf.Floor(_elapsed / 90f)));
		}
	}

	private int GetEnemyCap()
	{
		int swarmSteps = (int)Mathf.Floor(_elapsed / 90f);
		return Mathf.CeilToInt(MaxEnemies * Mathf.Pow(1f + SwarmGrowthPerNinetySeconds, swarmSteps));
	}

	private void UpdateCombat(float delta)
	{
		Vector2 mouseOffset = _owner.GetGlobalMousePosition() - _player;
		if (mouseOffset.LengthSquared() > 4f)
		{
			_aimDirection = mouseOffset.Normalized();
		}

		_shootTimer -= delta;

		if (_shootTimer <= 0f && _enemies.Count > 0)
		{
			AutoShoot();
			_shootTimer = Mathf.Max(.13f, .38f - _level * .018f);
		}
	}

	private void SpawnEnemy(bool initial)
	{
		Vector2 size = _owner.GetViewportRect().Size;
		Vector2 pos;

		int side = _rng.RandiRange(0, 3);
		pos = side switch
		{
			0 => new Vector2(-24f, _rng.RandfRange(105f, size.Y)),
			1 => new Vector2(size.X + 24f, _rng.RandfRange(105f, size.Y)),
			2 => new Vector2(_rng.RandfRange(0f, size.X), 85f),
			_ => new Vector2(_rng.RandfRange(0f, size.X), size.Y + 24f)
		};

		if (initial)
		{
			pos = _player + Vector2.FromAngle(_rng.RandfRange(0f, Mathf.Tau)) * _rng.RandfRange(180f, 340f);
		}

		_enemies.Add(new Enemy
		{
			Pos = pos,
			Hp = Mathf.Pow(EnemyHpScalePerMinute, Mathf.Floor(_elapsed / 60f)),
			Speed = _rng.RandfRange(42f, 70f) + _elapsed * .35f,
			Phase = _rng.RandfRange(0f, Mathf.Tau)
		});
	}

	private void UpdateEnemies(float delta)
	{
		foreach (Enemy enemy in _enemies)
		{
			Vector2 direction = (_player - enemy.Pos).Normalized();
			enemy.Pos += direction * enemy.Speed * delta;
			enemy.Phase += delta * 4f;

			if (enemy.Pos.DistanceTo(_player) < 25f)
			{
				_hp -= 22f * delta;
				if (_hp <= 0f)
				{
					_gameOver = true;
				}
			}
		}
	}

	private void AutoShoot()
	{
		if (_enemies.Count > 0)
		{
			int burstLevel = _level / 5;
			int bulletCount = 1 + burstLevel * 2;
			if (_shotArc > 0f)
			{
				// Golden power-ups eventually create one projectile for every degree.
				bulletCount = Mathf.Max(bulletCount, Mathf.CeilToInt(_shotArc));
			}
			float spread = .14f;

			for (int i = 0; i < bulletCount; i++)
			{
				float offset;
				if (_shotArc > 0f)
				{
					float arcRadians = Mathf.DegToRad(Mathf.Min(_shotArc, 360f));
					float arcPosition = bulletCount == 1 ? .5f : (float)i / bulletCount;
					offset = -arcRadians * .5f + arcRadians * arcPosition;
				}
				else
				{
					offset = (i - burstLevel) * spread;
				}

				Vector2 direction = _aimDirection.Rotated(offset);
				_bullets.Add(new Bullet
				{
					Pos = _player + direction * 20f,
					Velocity = direction * 570f,
					Life = 1.1f,
					Damage = _playerDamage
				});
			}
		}
	}

	private void UpdateBullets(float delta)
	{
		for (int i = _bullets.Count - 1; i >= 0; i--)
		{
			Bullet bullet = _bullets[i];
			bullet.Pos += bullet.Velocity * delta;
			bullet.Life -= delta;

			bool removed = bullet.Life <= 0f;

			for (int j = _enemies.Count - 1; j >= 0 && !removed; j--)
			{
				Enemy enemy = _enemies[j];
				if (bullet.Pos.DistanceTo(enemy.Pos) < 17f)
				{
					enemy.Hp -= bullet.Damage;
					Burst(enemy.Pos, new Color("#ffcf5a"), 5);
					removed = true;

					if (enemy.Hp <= 0f)
					{
						bool goldenDrop = _rng.Randf() < .06f;
						_gems.Add(new Gem { Pos = enemy.Pos, Value = 1, IsGolden = goldenDrop });
						_kills++;
						_enemies.RemoveAt(j);
					}
				}
			}

			if (removed)
			{
				_bullets.RemoveAt(i);
			}
		}
	}

	private void UpdateGems(float delta)
	{
		for (int i = _gems.Count - 1; i >= 0; i--)
		{
			Gem gem = _gems[i];
			float distance = _player.DistanceTo(gem.Pos);

			if (distance < 125f)
			{
				gem.Pos = gem.Pos.MoveToward(_player, (520f - distance) * delta);
			}

			if (gem.Pos.DistanceTo(_player) < 20f)
			{
				if (gem.IsGolden)
				{
					_shotArc = Mathf.Min(360f, _shotArc + 45f);
					Burst(_player, new Color("#ffc928"), 12);
					_gems.RemoveAt(i);
					continue;
				}

				_xp += gem.Value;
				_gems.RemoveAt(i);

				if (_xp >= _nextLevel)
				{
					_xp -= _nextLevel;
					_level++;
					_playerDamage += 0.25f;
					_nextLevel = 7 + _level * 5;
					_maxHp += 5f;
					_hp = Mathf.Min(_maxHp, _hp + 25f);
					_levelFlash = 2f;
				}
			}
		}
	}

	private void UpdateSparks(float delta)
	{
		for (int i = _sparks.Count - 1; i >= 0; i--)
		{
			Spark spark = _sparks[i];
			spark.Pos += spark.Velocity * delta;
			spark.Life -= delta;

			if (spark.Life <= 0f)
			{
				_sparks.RemoveAt(i);
			}
		}
	}

	private void Burst(Vector2 pos, Color color, int amount)
	{
		for (int i = 0; i < amount; i++)
		{
			_sparks.Add(new Spark
			{
				Pos = pos,
				Velocity = Vector2.FromAngle(_rng.RandfRange(0f, Mathf.Tau)) * _rng.RandfRange(30f, 105f),
				Life = .35f,
				Color = color
			});
		}
	}

	private void ResetGame()
	{
		_enemies.Clear();
		_bullets.Clear();
		_gems.Clear();
		_sparks.Clear();

		_player = _owner.GetViewportRect().Size * .5f;
		_aimDirection = Vector2.Right;
		_hp = _maxHp = 100f;
		_xp = 0;
			_level = 1;
			_playerDamage = 1f;
			_shotArc = 0f;
		_nextLevel = 8;
		_kills = 0;
		_elapsed = 0f;
		_gameOver = false;

		for (int i = 0; i < 12; i++)
		{
			SpawnEnemy(initial: true);
		}
	}

	private sealed class Enemy
	{
		public Vector2 Pos;
		public float Hp;
		public float Speed;
		public float Phase;
	}

	private sealed class Bullet
	{
		public Vector2 Pos;
		public Vector2 Velocity;
		public float Life;
		public float Damage;
	}

	private sealed class Gem
	{
		public Vector2 Pos;
		public int Value;
		public bool IsGolden;
	}

	private sealed class Spark
	{
		public Vector2 Pos;
		public Vector2 Velocity;
		public float Life;
		public Color Color;
	}
}
