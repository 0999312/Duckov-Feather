namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 控制角色切换事件。桥接游戏原生 <c>LevelManager.OnControllingCharacterChanged</c> 静态事件
    /// （原生签名 <c>Action&lt;CharacterMainControl&gt;</c>，仅 1 参：当前控制角色）。
    /// 原事件不带旧角色，OldCharacter 恒为 null（保留字段以兼容多参数语义）。
    /// </summary>
    public sealed class ControllingCharacterChangedEvent : Event
    {
        /// <summary>切换前的控制角色（原生事件无此参数，恒为 null）。</summary>
        public CharacterMainControl? OldCharacter { get; }

        /// <summary>当前的（新的）控制角色。</summary>
        public CharacterMainControl? NewCharacter { get; }

        public ControllingCharacterChangedEvent(CharacterMainControl? oldCharacter, CharacterMainControl? newCharacter)
        {
            OldCharacter = oldCharacter;
            NewCharacter = newCharacter;
        }
    }
}
