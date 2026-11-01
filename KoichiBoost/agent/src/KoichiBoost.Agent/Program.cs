using KoichiBoost.Agent;

// Koichi Boost — modo executável.
//  • Duplo clique (sem argumentos): abre o menu interativo.
//  • Com argumentos: modo linha de comando (veja --help).
// O app nunca se auto-eleva e nunca altera nada sem a sua confirmação.

Ui.Init();
var a = new Args(args);

try
{
    if (a.Flag("--help") || a.Flag("-h") || a.Command is "help") { PrintHelp(); return 0; }

    return a.Command switch
    {
        null when a.Count == 0 => Menu.Run(a),
        null or "diagnose" => Commands.Diagnose(a),
        "optimize" => Commands.Optimize(a),
        "restore" => Commands.Restore(a),
        "close-apps" => Commands.CloseApps(a),
        "tips" => Commands.Tips(),
        _ => PrintHelp()
    };
}
catch (Exception ex)
{
    Ui.Write($"\nErro inesperado: {ex.Message}", ConsoleColor.Red);
    if (a.Count == 0) Ui.Pause();
    return 1;
}

static int PrintHelp()
{
    Console.WriteLine("""
koichi-agent                      Abre o menu interativo (duplo clique)
koichi-agent diagnose             Analisa o PC e gera relatório (.md/.json)
koichi-agent optimize             Mostra o plano, pede confirmação e aplica ajustes com backup
koichi-agent restore              Desfaz ajustes aplicados anteriormente
koichi-agent close-apps           Fecha (com confirmação) apps de segundo plano
koichi-agent tips                 Configurações recomendadas dentro do VALORANT

Opções: --sample-seconds N  --hz N  --res WxH  --out PASTA  --sensors  --yes  --last  --all  --session ID  --no-open
Nenhum ajuste é feito sem confirmação. Todos os ajustes são reversíveis com 'restore'.
""");
    return 0;
}
