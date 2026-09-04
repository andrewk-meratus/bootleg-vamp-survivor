extends Node2D

const PLAYER_SPEED := 265.0
const ARENA_MARGIN := 38.0
const MAX_ENEMIES := 90

var player := Vector2.ZERO
var enemies: Array[Dictionary] = []
var bullets: Array[Dictionary] = []
var gems: Array[Dictionary] = []
var sparks: Array[Dictionary] = []
var elapsed := 0.0
var shoot_timer := 0.0
var spawn_timer := 0.0
var hp := 100.0
var max_hp := 100.0
var xp := 0
var level := 1
var next_level := 8
var kills := 0
var game_over := false
var level_flash := 0.0
var rng := RandomNumberGenerator.new()

func _ready() -> void:
	rng.randomize()
	player = get_viewport_rect().size * 0.5
	for i in 12:
		spawn_enemy(true)
	queue_redraw()

func _process(delta: float) -> void:
	if game_over:
		if Input.is_key_pressed(KEY_R):
			reset_game()
		queue_redraw()
		return

	elapsed += delta
	level_flash = maxf(0.0, level_flash - delta)
	move_player(delta)
	spawn_timer -= delta
	if spawn_timer <= 0.0 and enemies.size() < MAX_ENEMIES:
		spawn_enemy(false)
		spawn_timer = maxf(0.16, 0.72 - elapsed * 0.006)

	shoot_timer -= delta
	if shoot_timer <= 0.0 and not enemies.is_empty():
		auto_shoot()
		shoot_timer = maxf(0.13, 0.38 - level * 0.018)

	update_enemies(delta)
	update_bullets(delta)
	update_gems(delta)
	update_sparks(delta)
	queue_redraw()

func move_player(delta: float) -> void:
	var direction := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_key_pressed(KEY_A): direction.x -= 1.0
	if Input.is_key_pressed(KEY_D): direction.x += 1.0
	if Input.is_key_pressed(KEY_W): direction.y -= 1.0
	if Input.is_key_pressed(KEY_S): direction.y += 1.0
	if direction.length() > 1.0: direction = direction.normalized()
	player += direction * PLAYER_SPEED * delta
	var size := get_viewport_rect().size
	player.x = clampf(player.x, ARENA_MARGIN, size.x - ARENA_MARGIN)
	player.y = clampf(player.y, 100.0, size.y - ARENA_MARGIN)

func spawn_enemy(initial: bool) -> void:
	var size := get_viewport_rect().size
	var side := rng.randi_range(0, 3)
	var pos := Vector2.ZERO
	if side == 0: pos = Vector2(-24.0, rng.randf_range(105.0, size.y))
	if side == 1: pos = Vector2(size.x + 24.0, rng.randf_range(105.0, size.y))
	if side == 2: pos = Vector2(rng.randf_range(0.0, size.x), 85.0)
	if side == 3: pos = Vector2(rng.randf_range(0.0, size.x), size.y + 24.0)
	if initial: pos = player + Vector2.from_angle(rng.randf_range(0.0, TAU)) * rng.randf_range(180.0, 340.0)
	enemies.append({"pos": pos, "hp": 1 + int(elapsed / 35.0), "speed": rng.randf_range(42.0, 70.0) + elapsed * 0.35, "phase": rng.randf_range(0.0, TAU)})

func update_enemies(delta: float) -> void:
	for i in range(enemies.size() - 1, -1, -1):
		var enemy: Dictionary = enemies[i]
		var pos: Vector2 = enemy.pos
		var target: Vector2 = (player - pos).normalized()
		pos += target * float(enemy.speed) * delta
		enemy.pos = pos
		enemy.phase = float(enemy.phase) + delta * 4.0
		if pos.distance_to(player) < 25.0:
			hp -= 22.0 * delta
			if hp <= 0.0: game_over = true
		enemies[i] = enemy

func auto_shoot() -> void:
	var nearest := -1
	var nearest_dist := INF
	for i in enemies.size():
		var distance: float = player.distance_to(enemies[i].pos)
		if distance < nearest_dist:
			nearest = i
			nearest_dist = distance
	if nearest >= 0:
		bullets.append({"pos": player, "velocity": (enemies[nearest].pos - player).normalized() * 570.0, "life": 1.1, "damage": 1 + level / 4})

func update_bullets(delta: float) -> void:
	for i in range(bullets.size() - 1, -1, -1):
		var bullet: Dictionary = bullets[i]
		bullet.pos += bullet.velocity * delta
		bullet.life -= delta
		var removed: bool = float(bullet.life) <= 0.0
		for j in range(enemies.size() - 1, -1, -1):
			if not removed and bullet.pos.distance_to(enemies[j].pos) < 17.0:
				enemies[j].hp -= bullet.damage
				burst(enemies[j].pos, Color("#ffcf5a"), 5)
				removed = true
				if enemies[j].hp <= 0:
					gems.append({"pos": enemies[j].pos, "value": 1})
					kills += 1
					enemies.remove_at(j)
		if removed: bullets.remove_at(i)
		else: bullets[i] = bullet

func update_gems(delta: float) -> void:
	for i in range(gems.size() - 1, -1, -1):
		var gem: Dictionary = gems[i]
		var distance: float = player.distance_to(gem.pos)
		if distance < 125.0: gem.pos = gem.pos.move_toward(player, (520.0 - distance) * delta)
		if gem.pos.distance_to(player) < 20.0:
			xp += int(gem.value)
			gems.remove_at(i)
			if xp >= next_level:
				xp -= next_level
				level += 1
				next_level = 7 + level * 5
				max_hp += 5.0
				hp = minf(max_hp, hp + 25.0)
				level_flash = 2.0
		else: gems[i] = gem

func burst(pos: Vector2, color: Color, amount: int) -> void:
	for i in amount: sparks.append({"pos": pos, "velocity": Vector2.from_angle(rng.randf_range(0.0, TAU)) * rng.randf_range(30.0, 105.0), "life": 0.35, "color": color})

func update_sparks(delta: float) -> void:
	for i in range(sparks.size() - 1, -1, -1):
		var spark: Dictionary = sparks[i]
		spark.pos += spark.velocity * delta
		spark.life -= delta
		if spark.life <= 0.0: sparks.remove_at(i)
		else: sparks[i] = spark

func reset_game() -> void:
	enemies.clear(); bullets.clear(); gems.clear(); sparks.clear()
	player = get_viewport_rect().size * 0.5
	hp = 100.0; max_hp = 100.0; xp = 0; level = 1; next_level = 8; kills = 0; elapsed = 0.0; game_over = false
	for i in 12: spawn_enemy(true)

func _draw() -> void:
	var size := get_viewport_rect().size
	draw_rect(Rect2(Vector2.ZERO, size), Color("#090d18"))
	for x in range(0, int(size.x), 48): draw_line(Vector2(x, 78), Vector2(x, size.y), Color(0.10, 0.14, 0.23, 0.42), 1.0)
	for y in range(96, int(size.y), 48): draw_line(Vector2(0, y), Vector2(size.x, y), Color(0.10, 0.14, 0.23, 0.42), 1.0)
	draw_rect(Rect2(0, 0, size.x, 78), Color("#111a2d"))
	draw_string(ThemeDB.fallback_font, Vector2(24, 34), "NIGHT SHIFT", HORIZONTAL_ALIGNMENT_LEFT, -1, 24, Color("#f4f0df"))
	draw_string(ThemeDB.fallback_font, Vector2(24, 59), "SURVIVE THE SWARM", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("#7f8ca8"))
	draw_string(ThemeDB.fallback_font, Vector2(size.x - 265, 32), "WASD / ARROWS   AUTO-FIRE", HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color("#aab5cd"))
	draw_string(ThemeDB.fallback_font, Vector2(size.x - 265, 57), "TIME  %05.1f" % elapsed, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("#f4f0df"))
	for gem in gems: draw_colored_polygon(PackedVector2Array([gem.pos + Vector2(0, -7), gem.pos + Vector2(7, 0), gem.pos + Vector2(0, 7), gem.pos + Vector2(-7, 0)]), Color("#6fe7c8"))
	for bullet in bullets: draw_circle(bullet.pos, 5.0, Color("#fff3a6")); draw_circle(bullet.pos, 9.0, Color(1.0, 0.75, 0.25, 0.16))
	for enemy in enemies:
		var p: Vector2 = enemy.pos
		var bob := sin(float(enemy.phase)) * 2.0
		draw_circle(p + Vector2(0, bob), 14.0, Color("#321e3d")); draw_circle(p + Vector2(0, bob), 10.0, Color("#d94c69"))
		draw_circle(p + Vector2(-4, -2 + bob), 2.0, Color("#ffe29a")); draw_circle(p + Vector2(4, -2 + bob), 2.0, Color("#ffe29a"))
	for spark in sparks: draw_circle(spark.pos, 3.0, Color(spark.color, clampf(float(spark.life) * 3.0, 0.0, 1.0)))
	# Player and its readable shadow/outline.
	draw_circle(player + Vector2(0, 5), 20.0, Color(0, 0, 0, 0.35)); draw_circle(player, 18.0, Color("#273b72")); draw_circle(player, 13.0, Color("#65b8ff")); draw_circle(player + Vector2(-4, -4), 4.0, Color("#e8f5ff"))
	draw_rect(Rect2(24, size.y - 40, 210, 12), Color("#202941")); draw_rect(Rect2(24, size.y - 40, 210.0 * hp / max_hp, 12), Color("#e95b73")); draw_string(ThemeDB.fallback_font, Vector2(24, size.y - 49), "HP %03d / %03d" % [int(hp), int(max_hp)], HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("#dfe7f4"))
	draw_string(ThemeDB.fallback_font, Vector2(size.x - 210, size.y - 47), "LVL %02d     KILLS %03d" % [level, kills], HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color("#dfe7f4"))
	draw_rect(Rect2(size.x * 0.5 - 130, size.y - 25, 260, 9), Color("#202941")); draw_rect(Rect2(size.x * 0.5 - 130, size.y - 25, 260.0 * xp / next_level, 9), Color("#6fe7c8"))
	if level_flash > 0.0: draw_string(ThemeDB.fallback_font, Vector2(size.x * 0.5 - 110, 145), "LEVEL UP  + MAX HP", HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color("#6fe7c8"))
	if game_over:
		draw_rect(Rect2(0, 0, size.x, size.y), Color(0.02, 0.03, 0.07, 0.72))
		draw_string(ThemeDB.fallback_font, Vector2(size.x * 0.5 - 115, size.y * 0.5 - 18), "YOU GOT SWARMED", HORIZONTAL_ALIGNMENT_LEFT, -1, 25, Color("#ff6b7f"))
		draw_string(ThemeDB.fallback_font, Vector2(size.x * 0.5 - 95, size.y * 0.5 + 25), "PRESS R TO RISE AGAIN", HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color("#f4f0df"))
