namespace Combat.Runtime.Commands
{
    public interface ICombatCommand
    {
        void Execute(BattleContext context);
    }
}

