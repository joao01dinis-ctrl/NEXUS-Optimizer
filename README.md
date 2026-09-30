# NEXUS Optimizer

Projeto open source experimental, sob [licença MIT](LICENSE).
Consulte a [política de privacidade](PRIVACY.md), a [Code signing policy](CODE_SIGNING.md)
e o [guia de contribuição](CONTRIBUTING.md). A candidatura à SignPath está em preparação;
o projeto ainda não tem patrocínio ou assinatura aprovada.

O workflow **Windows build and tests** compila, testa e disponibiliza um artefacto
portátil sem assinatura. Tags `v*` publicam uma versão experimental após os testes.

Primeira versão Windows x64 em C# 14, .NET 10 e WinUI 3 / Windows App SDK. Interface em português, sem privilégios de administrador.

**Estado da validação neste PC:** build Release aprovado sem avisos e 11 testes aprovados. A publicação inclui os runtimes e recursos XAML. O teste de arranque ficou bloqueado por uma política de Controlo de Aplicações do Windows ao carregar `Nexus.Core.dll` (`0x800711C7`); a utilização completa da interface ainda não foi validada. Nenhuma proteção foi alterada. Ver [VALIDATION.md](VALIDATION.md).

## Funcionalidades

- Dashboard com utilização global de CPU e RAM, atualizado periodicamente.
- Identificação CPU/GPU via WMI, memória física e volumes fixos com espaço disponível.
- MVVM com CommunityToolkit.Mvvm; logs Serilog com retenção de sete ficheiros diários.
- Perfis Normal (2 s), Gaming (5 s) e Trabalho (3 s): controlam apenas o intervalo de monitorização desta aplicação. Não prometem ganhos de FPS.
- SQLite guarda perfil ativo e histórico; undo persistente e transacional, do mais recente para o mais antigo. Aplicar o mesmo perfil não cria entradas duplicadas.
- Nenhuma alteração ao registo, serviços, Defender, firewall, rede, energia ou ficheiros pessoais.

## Compilar e executar

Requisitos: Windows 10 2004+ / Windows 11 x64 e SDK .NET 10.0.401 (ou patch posterior da mesma banda). Visual Studio com ferramentas WinUI é opcional para editar; o build usa os pacotes NuGet. Internet necessária no primeiro restore.

```powershell
./build.ps1
./build.ps1 -Publish
./artifacts/app/Nexus.UI.exe
```

Se o SDK não estiver no PATH, indique `-DotnetPath 'C:\caminho\dotnet.exe'`. O pacote publicado inclui .NET e Windows App SDK. Distribua a pasta completa, não apenas o executável.

```powershell
dotnet build NexusOptimizer.sln -c Release
dotnet test src/Nexus.Tests/Nexus.Tests.csproj -c Release
dotnet publish src/Nexus.UI/Nexus.UI.csproj -c Release -r win-x64 --self-contained true -o artifacts/app
```

## Módulos

| Projeto | Responsabilidade / estado |
|---|---|
| UI | WinUI, dashboard, view model, comandos e ciclo de atualização |
| Core | Modelos e contratos independentes da interface |
| Hardware | WMI e APIs Win32, leituras sem alterações ao sistema |
| Optimization | Validação e aplicação dos perfis conservadores |
| Gaming | Ponto de extensão; FPS/PresentMon ainda não implementado |
| Diagnostics | Configuração e escrita de logs Serilog |
| Benchmark | Ponto de extensão; testes de desempenho ainda não implementados |
| Network | Ponto de extensão; diagnóstico de rede ainda não implementado |
| Storage | SQLite, esquema inicial, perfis, histórico e transações |
| Restore | Comando de undo; reverte apenas preferências da app |
| Service | Serviço de monitorização dentro do processo; não instala serviço Windows |
| Tests | Testes de persistência, validação, undo e amostra real de memória |

## Dados e limitações

Dados locais em `%LOCALAPPDATA%\NexusOptimizer`: `nexus.db` e `logs\nexus-*.log`. Não existe telemetria nem envio de dados. Os logs podem incluir nomes de hardware e mensagens do Windows. Para repor as preferências, feche a app e remova a base de dados e os seus ficheiros WAL/SHM dessa pasta.

A primeira leitura CPU necessita de uma segunda amostra para calcular a percentagem. GPU é apenas identificação, sem carga/temperatura/FPS. Discos são volumes lógicos fixos, não inventário físico SMART. WMI pode estar indisponível: a interface mostra “Indisponível” e regista o erro. O dashboard apresenta as últimas 100 alterações, mantendo todas na base de dados. Fechar a janela termina a monitorização.

O esquema SQLite está na versão 1. Alterações futuras exigem migrações versionadas. Para futuras otimizações do Windows, capturar o estado anterior antes de cada ação, validar a reversibilidade e implementar recuperação de falhas; o undo atual não restaura o sistema operativo.

O executável desta versão de desenvolvimento não tem assinatura de distribuição. Ambientes que exigem aplicações confiáveis podem bloquear os ficheiros gerados. A distribuição nesses ambientes requer uma assinatura confiável ou o processo de aprovação definido pelo administrador; este projeto não desativa nem contorna essa política.

Para testes isolados, a variável `NEXUS_DATA_DIR` permite escolher outra pasta de dados. O argumento `--smoke-test`, usado com essa variável, tenta abrir a janela, ler métricas e gravar `dashboard.png` e `smoke-test.json` antes de fechar. Não executa alterações aos perfis nem ao Windows.

## Verificação manual

1. Abrir o executável e confirmar CPU/GPU/RAM/volumes e amostras atualizadas.
2. Aplicar Gaming e confirmar perfil e histórico; fechar e reabrir para verificar persistência.
3. Aplicar Trabalho, desfazer duas vezes e confirmar Normal.
4. Confirmar logs e ausência de pedidos de elevação.

Referências: [WinUI unpackaged](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app), [Windows App SDK self-contained](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).
