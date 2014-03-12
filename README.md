Description
===========
This mod provides basic statistics on each player as well as an encounter (boss or invasion) DPS meter. It can also be used to provide status updates of encounters. For instance, messages for Frost Moon will display the wave number, points until next wave, overall percentage of the encounter completed, time in minutes/seconds left of night, and percentage of night completed.

Commands
--------
/stat [playername] - Display most stats on a player if name provided, or yourself by default.

    Includes: Kills, Damage done, Max Hit, Crits, Crit%, Damage received, MaxHit, Times Critted, Amount Healed, Times Healed, Mana Recovered, Times Mana Recovered

/stat playername crit|crits|critical - Display game's information on melee/magic/ranged crits.

/boss [previous number] - Displays last boss battle stats.
/battle [previous number] - Displays last invasion battle stats.

    Duration in minutes/seconds
    Last Hit - Who got the 'kill' and for how much damage.
    For each player that did damage
        Total Damage contributed
        Percentage of Damage contributed overall
        DPS (damage per second) calculated.
    [previous number] returns previous battle report instead of latest.

/bosses - Provides number of recorded boss battles in memory.
/bosses clear - Clears records of boss battles. Records infinite (based on RAM) so clearing might be needed

/invasion
/event - Reports the progress of the current invasion. This is automatically subscription based but you can type it in manually as well

    Goblin Army
        Distance away
        Time in seconds until Army arrives
        On Arrival:
            Goblins Killed
            Invasion Size
            Percent Completed (kills vs remaining)
            Kills Remaining (to 'defeat' army)
    Pumpkin Moon & Frost Moon
        Current Wave / Max Waves
        Current Points / Max Points (for next wave)
        Overall Event Completion Percent (getting to final wave)
        Time left until morning - in minutes and seconds and percent
        On Final Wave - Accumulated points (keeps counting), Time until morning

/events - Displays number of events recorded in memory
/events clear - Clears the recorded events from memory

/wave - Displays debug info: Wave Count and Wave Kills
/wave number - Sets the current wave to provided number. Allows skipping to final wave

/unsub
/unsubscribe - Unsubscribes a player from receiving continuous Event reports

/sub
/subscribe - Subscribes a player to automatically receive Event reports during invasions (see /event)

/tliteral - Displays debug info: integer value of the Game Time variable
/ttm - Displays the time until morning in minutes, seconds, and overall percentage completed
/ttn - Displays the time until night in minutes, seconds, and overall percentage completed.
