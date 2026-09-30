# Validação — 29/09/2026

## Concluído

- SDK Microsoft .NET 10.0.401 local, arquivo oficial validado por SHA-512.
- Solução com os 12 projetos solicitados, compilada em Release x64.
- Build: 0 erros e 0 avisos.
- 11 testes xUnit aprovados, 0 falhados e 0 ignorados.
- Testes cobrem perfil inicial, persistência, mudanças sem efeito, rejeição de perfis inválidos, undo em ordem inversa, undo após reinício, nova alteração após undo, intervalos conservadores, RAM real, identificação de hardware e CPU após duas amostras.
- Publicação self-contained em `artifacts/app`, incluindo .NET, Windows App SDK, SQLite nativo, XBF e PRI.
- Serilog registou corretamente a falha de arranque.
- Repositório Git local e instruções de compilação incluídos.

## Bloqueio de execução

O teste do executável publicado encontrou primeiro recursos XAML em falta; a publicação foi corrigida para incluir `App.xbf`, `MainWindow.xbf` e `Nexus.UI.pri`.

Na tentativa seguinte, a criação da janela avançou até à construção do view model, onde o Windows recusou carregar `Nexus.Core.dll`:

```
System.IO.FileLoadException
Uma política de Controlo de Aplicações bloqueou este ficheiro. (0x800711C7)
```

Esse bloqueio impede confirmar o dashboard completo, interações dos botões e captura visual no ambiente atual. Não foram desativados Smart App Control, Defender, firewall, políticas de execução ou outros mecanismos de segurança. É necessária uma via de distribuição/assinatura aceite pela política do dispositivo para completar essa validação.

Os testes automatizados de Core, Hardware, Storage, Optimization e Restore executaram com sucesso antes desta tentativa. Isto não substitui a verificação visual da interface.

## Evidência local

- `artifacts/tests/tests.trx`: resultados dos testes.
- `artifacts/validation/build.log`: build final.
- `artifacts/validation/publish.log`: publicação.
- `artifacts/validation/startup.log`: diagnóstico do arranque bloqueado.

As pastas de artefactos não são incluídas no histórico Git. A solução, o código, os testes e os scripts são versionados.
