using System;

namespace Combat.Runtime.Commands
{
    public sealed class CommandBuffer
    {
        private ICombatCommand[] _commands = new ICombatCommand[8];
        private int _count;

        public int Count => _count;

        public void Enqueue(ICombatCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            if (_count == _commands.Length)
            {
                int newSize = _commands.Length * 2;
                var newCommands = new ICombatCommand[newSize];
                Array.Copy(_commands, newCommands, _commands.Length);
                _commands = newCommands;
            }

            _commands[_count] = command;
            _count++;
        }

        public void ApplyAll(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            for (int i = 0; i < _count; i++)
            {
                _commands[i].Execute(context);
                _commands[i] = null;
            }

            _count = 0;
        }
    }
}

