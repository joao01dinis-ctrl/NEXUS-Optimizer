# Melhorias aplicadas a partir do estudo — NEXUS 0.7

13/10/2026. Implementação própria, gratuita, baseada no estudo fornecido. O estudo é uma proposta, não uma auditoria nem evidência de ganhos neste PC. Não foi reutilizado código TL Optimizer. Não é declarada paridade com o seu motor interno.

## Smart Boost

PC Boost recolhe cinco amostras de CPU/RAM com intervalos de dois segundos, depois de preparar o contador CPU. Avalia cinco preferências de interface e sete decisões de diagnóstico. As regras próprias têm versão `1.0.0`, condição, decisão, motivo, método de verificação e reversão. Não são 74 regras TL nem 12 tweaks de desempenho.

- Recomendado: pressão observada em pelo menos 80% das cinco amostras; CPU ≥80% ou RAM ≥85%. Só preferências de interface acessíveis e diferentes ficam selecionadas, para revisão explícita.
- Ignorar: configuração já presente, valores inacessíveis, observações inválidas/incompletas ou falta de condição que justifique recomendar a mudança.
- Testar manualmente: preferências opcionais no objetivo Competitivo, revisão de consumidores, energia, prioridade, arranque, espaço e driver. Estes conselhos não executam ações automaticamente.

Os limiares são heurísticas transparentes; não foram calibrados por hardware e não provam um limite CPU/GPU, paginação ou impacto de uma aplicação nos FPS. Uma ocorrência isolada não ativa uma recomendação de pressão sustentada. A escolha expira após cinco minutos; mudanças de perfil invalidam-na. Antes de escrever, o conjunto verifica novamente os valores esperados e mantém a recuperação de escritas interrompidas da 0.6.

A análise inclui nomes CPU/GPU, versão Windows, driver gráfico instalado, RAM disponível, espaço nos volumes, alimentação/poupança e CPU das aplicações acessíveis. Não instala drivers nem classifica uma versão como desatualizada sem consultar o fabricante. Exporta JSON apenas para a pasta escolhida, com nomes de hardware/aplicações; não envia dados. Motivos da análise e comparações ficam no histórico SQLite.

## Perfis e sessões

Balanceado é o predefinido em PC Boost e Game Mode. Uma sessão sem ajustes acompanha o processo sem escrever preferências. Competitivo permite reduzir transições; prioridade original permanece por defeito e AboveNormal exige escolha explícita. Streaming mantém prioridade original para partilhar recursos com transmissão/áudio. Personalizado continua disponível nas sessões. O antigo Extremo foi retirado: duplicava Competitivo sem benefício demonstrado.

Energia por sessão exige corrente e poupança desligada, ambas conhecidas. Automático mantém o plano se a condição não for satisfeita. Se uma sessão tiver mudado energia e a condição deixar de ser satisfeita, o acompanhamento tenta terminar e repor a sessão. A lista automática/perfil permanece fixa e autorizada apenas nesta abertura. Não encerra, suspende ou reduz a prioridade de OBS, Discord, áudio, anti-cheat ou processos de fundo.

O journal persistente anterior continua a guardar valores antes de escrever. Recuperação após falha é explícita na próxima abertura; sem serviço privilegiado instalado, não se promete restauro enquanto a NEXUS estiver fechada.

## FPS Lab

Mantém a importação de CSV PresentMon por aplicação/PID/fluxo. Acrescenta frametime médio e 0,1% low (pelo menos mil intervalos válidos), sem remover pausas. Capturas anteriores sem o novo campo continuam válidas e mostram indisponibilidade.

Cada captura pode ser classificada como Referência/Otimizado. A comparação de repetições exige três a vinte CSVs por grupo, hashes independentes em todo o conjunto, mesma aplicação/condições, pelo menos 20 segundos/300 intervalos, até um omitido e duração máxima/mínima até 1,2. Fluxos do mesmo CSV não contam como repetições independentes.

Mostra medianas e faixas entre testes para FPS, 1% low e P99. Quando as faixas sobrepõem ou as métricas discordam, diz inconclusivo/misto. Mesmo faixas separadas não demonstram causalidade; não são intervalos de confiança. A NEXUS não verifica automaticamente temperatura, driver, resolução ou cena declarados.

## Correspondência com as áreas do estudo

| Área | Situação nesta revisão |
|---|---|
| Hardware Scanner | CPU/GPU/RAM/discos, driver e alimentação consultados. Temperaturas, VRAM, GPU Busy e clocks ainda indisponíveis. |
| Smart Boost | Motor condicionado e versionado, decisão por regra, revisão e exportação. Sem aprendizagem automática ou regras por modelo de hardware. |
| Game Boost | Deteção por caminho escolhido, sessões, automático opt-in, energia condicionada e reposição. Sem gestão dinâmica de apps de fundo ou perfis persistidos por jogo. |
| FPS Lab | Capturas importadas, métricas e comparação repetida. Sem captura integrada/overlay ou sessão FiveM real validada. |
| Restore | Valores persistentes, conflitos preservados, restauro global/sessões/recuperação. Sem serviço que restaure com a app fechada. |
| CPU Manager | Medição passiva por processo. Não decide prioridades dinâmicas ou afinidade. |
| GPU Manager | Identificação/driver e atalho Windows. Sem alteração de configurações de fabricante. |
| RAM Monitor | Uso/disponível/processos; pressão sustentada. Não mede page faults ou purga standby memory. |
| Network Lab | Mantém DNS/ICMP/perda/jitter e adaptadores. Não aplica TCP genérico nem promete baixar ping. |
| Cleaner | Mantém revisão/Lixeira e limites. Nada é apagado pelo Smart Boost. |
| Startup Manager | Revisão nas Definições oficiais do Windows; não altera entradas de arranque pela NEXUS. |
| Diagnostics/Stability | Logs, relatório, verificação de desvios e recuperação. Sem diagnóstico térmico ou atribuição automática da causa de crashes. |

Não foram acrescentados Defender/Update/VBS/HPET/BCDEdit, serviços em massa, limpeza de registo, pagefile desativado, Real Time, overclock ou instalação automática de BIOS/drivers. Não é necessário executar a interface com privilégios permanentes de administrador.

## Verificação

Resultados concretos e limitações em [VALIDATION.md](VALIDATION.md). Build/testes não substituem execução da interface, certificação MSIX ou testes FiveM. A assinatura aceite pelo Windows continua a ser uma pendência separada do motor de otimização.

Referências técnicas consultadas: [estado de alimentação Win32](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getsystempowerstatus), [identificação do driver gráfico](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller), [métricas e CSV PresentMon](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md), [gestão de aplicações de arranque](https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows).
