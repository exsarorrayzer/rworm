import subprocess
import shlex

def execute_command(command: str) -> str:
    if not command:
        return "No command provided"
    
    try:
        result = subprocess.run(
            command,
            shell=True,
            capture_output=True,
            text=True,
            timeout=30,
            cwd=None,
            env=None
        )
        
        output = result.stdout
        if result.stderr:
            output += f"\n[STDERR]\n{result.stderr}"
            
        return output if output else "[No output]"
        
    except subprocess.TimeoutExpired:
        return "[Command timed out after 30 seconds]"
    except Exception as e:
        return f"[Error: {str(e)}]"